#include <stdint.h>
#include <stdio.h>
#include <sys/mman.h>
#include <unistd.h>

/* Constants */
#define GC_INITIAL_THRESHOLD (1024 * 1024 * 8)  // 8MB initial threshold
#define GC_MIN_ALLOC 4096                       // Minimum allocation size

/* Allocation metadata structures */
typedef struct alloc_block {
    uint8_t mark;              // Mark bit for GC
    uint8_t padding[7];        // Padding for alignment
    int64_t size;              // Size of the allocation
    struct alloc_block* next;  // Next allocation in the list
} alloc_block_t;

typedef struct free_block {
    int64_t size;             // Size of the free block
    struct free_block* next;  // Next free block in the list
} free_block_t;

/* Global variables */
static alloc_block_t* alloc_list_head = NULL;
static free_block_t* free_list_head = NULL;
static void* stack_start = NULL;
static int64_t free_list_size = 0;
static int64_t mark_count = 0;

/* GC metrics */
static int64_t gc_bytes_allocated = 0;
static int64_t gc_trigger_threshold = GC_INITIAL_THRESHOLD;

/* Forward declarations */
static void* sys_mmap(size_t size);
static void gc_collect(void);
static void gc_mark(void* ptr);
static void gc_mark_stack(void);
static void gc_sweep(void);
static int64_t max(int64_t a, int64_t b);
static void insert_into_free(free_block_t* block);
static void merge(free_block_t* block);

/* Initialize the garbage collector */
void gc_init(void) {
    // Save the current stack pointer as the start of the stack
    volatile unsigned long var = 0;
    stack_start = (void*)((unsigned long)&var + 4);
}

/* Allocate memory with garbage collection */
void* gc_alloc(int64_t size) {
    size += sizeof(alloc_block_t);  // Adjust for metadata size

    // Check if we need to trigger garbage collection
    if (gc_bytes_allocated > gc_trigger_threshold) {
        gc_collect();
    }

    gc_bytes_allocated += size;  // Adjust allocation counter

    // Search free list for a suitable block
    free_block_t* current = free_list_head;
    free_block_t* prev = NULL;

    while (current != NULL) {
        if (current->size >= size) {
            // Found a suitable block
            break;
        }
        prev = current;
        current = current->next;
    }

    if (current == NULL) {
        // No suitable block found, allocate a new one
        int64_t alloc_size = max(size, GC_MIN_ALLOC);
        void* memory = sys_mmap(alloc_size);

        free_block_t* new_block = (free_block_t*)memory;
        new_block->size = alloc_size - sizeof(free_block_t);
        new_block->next = NULL;

        insert_into_free(new_block);
        current = new_block;

        // Recalculate prev
        if (current == free_list_head) {
            prev = NULL;
        } else {
            prev = free_list_head;
            while (prev->next != current) {
                prev = prev->next;
            }
        }
    }

    // Use the block
    alloc_block_t* result;

    if (current->size > size) {
        // Block is larger than needed, split it
        result = (alloc_block_t*)((char*)current + current->size + sizeof(free_block_t) - size);
        current->size -= size;
    } else {
        // Use the entire block
        result = (alloc_block_t*)current;

        // Remove block from free list
        if (prev == NULL) {
            free_list_head = current->next;
        } else {
            prev->next = current->next;
        }

        free_list_size--;
    }

    // Initialize metadata
    result->mark = 0;
    result->size = size - sizeof(alloc_block_t);
    result->next = alloc_list_head;
    alloc_list_head = result;

    // Return pointer to usable memory
    return (void*)(result + 1);
}

/* Run garbage collection */
static void gc_collect(void) {
    printf("Reached threshold of %ld bytes. Starting GC\n", gc_bytes_allocated);
    gc_mark_stack();
    printf("\tMarking done. Objects marked is %ld\n", mark_count);
    gc_sweep();
    printf("\tSweep done. We now have %ld allocated bytes\n", gc_bytes_allocated);
    gc_trigger_threshold = max(gc_bytes_allocated * 2, GC_INITIAL_THRESHOLD);
    gc_bytes_allocated = 0;
    printf("\tThe next threshold is %ld allocated bytes\n", gc_trigger_threshold);
    printf("\tFree list size is %ld\n", free_list_size);
}

/* Mark phase of GC - scan stack for pointers */
static void gc_mark_stack(void) {
    mark_count = 0;
    void** current = (void**)&current;  // Approximate current stack position
    void** end = (void**)stack_start;

    while (current < end) {
        gc_mark(*current);
        current++;
    }
}

/* Mark a single object and recursively mark its contents */
static void gc_mark(void* ptr) {
    if (ptr == NULL)
        return;

    // Check if ptr points to a valid allocation
    alloc_block_t* block = alloc_list_head;
    while (block != NULL) {
        void* block_data = (void*)(block + 1);
        if (block_data == ptr) {
            // Found the block, mark it if not already marked
            if (block->mark == 0) {
                mark_count++;
                block->mark = 1;

                // Recursively mark all pointers in the object
                void** p = (void**)block_data;
                void** end = (void**)((char*)block_data + block->size);

                while (p < end) {
                    gc_mark(*p);
                    p++;
                }
            }
            return;
        }
        block = block->next;
    }
}

/* Sweep phase of GC - free unmarked objects */
static void gc_sweep(void) {
    alloc_block_t* current = alloc_list_head;
    alloc_block_t* prev = NULL;

    while (current != NULL) {
        if (current->mark == 0) {
            // Unmarked object, remove it from the allocation list
            alloc_block_t* next = current->next;

            if (prev == NULL) {
                alloc_list_head = next;
            } else {
                prev->next = next;
            }

            // Adjust allocated bytes counter
            gc_bytes_allocated -= (current->size + sizeof(alloc_block_t));

            // Add to free list
            free_block_t* free_block = (free_block_t*)current;
            free_block->size = current->size + sizeof(alloc_block_t) - sizeof(free_block_t);
            free_block->next = NULL;

            insert_into_free(free_block);

            current = next;
        } else {
            // Marked object, unmark it for next GC cycle
            current->mark = 0;
            prev = current;
            current = current->next;
        }
    }
}

/* Insert a block into the free list, maintaining address order */
static void insert_into_free(free_block_t* block) {
    if (free_list_head == NULL || block < free_list_head) {
        // Insert at head
        block->next = free_list_head;
        free_list_head = block;
        free_list_size++;
        merge(block);
        return;
    }

    // Find insertion point
    free_block_t* current = free_list_head;
    while (current->next != NULL && current->next < block) {
        current = current->next;
    }

    // Insert after current
    block->next = current->next;
    current->next = block;
    free_list_size++;

    // Try to merge adjacent blocks
    merge(current);
}

/* Merge a block with any adjacent blocks */
static void merge(free_block_t* block) {
    while (block->next != NULL) {
        char* block_end = (char*)block + block->size + sizeof(free_block_t);

        if (block_end == (char*)block->next) {
            // Blocks are adjacent, merge them
            free_list_size--;
            block->size += block->next->size + sizeof(free_block_t);
            block->next = block->next->next;
        } else {
            // No more adjacent blocks
            break;
        }
    }
}

/* Helper to map memory from the system */
static void* sys_mmap(size_t size) {
    void* result = mmap(NULL, size, PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

    if (result == MAP_FAILED) {
        _exit(1);  // Exit on failure
    }

    return result;
}

/* Return maximum of two values */
static int64_t max(int64_t a, int64_t b) {
    return (a > b) ? a : b;
}