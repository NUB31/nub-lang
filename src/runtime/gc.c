#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <sys/mman.h>

#define MINIMUM_THRESHOLD (1024 * 1024 * 8)
#define MINIMUM_BLOCK_SIZE 4096

typedef struct alloc_block {
    uint64_t mark;
    uint64_t size;
    struct alloc_block* next;
} alloc_block_t;

typedef struct free_block {
    uint64_t size;
    struct free_block* next;
} free_block_t;

static alloc_block_t* alloc_list_head = NULL;
static free_block_t* free_list_head = NULL;
static void* stack_start = NULL;
static int64_t free_list_size = 0;
static int64_t mark_count = 0;

/* Bytes allocated since last collect */
static int64_t bytes_allocated = 0;
/* Threshold for next collect */
static int64_t trigger_threshold = MINIMUM_THRESHOLD;

static void* sys_mmap(size_t size);
static void* get_sp(void);
static void gc_collect(void);
static void gc_mark(void* ptr);
static void gc_mark_stack(void);
static void gc_sweep(void);
static int64_t max(int64_t a, int64_t b);
static void insert_into_free(free_block_t* block);
static void merge(free_block_t* block);

void gc_init(void) {
    stack_start = get_sp();
}

/* Allocate memory with garbage collection */
void* gc_alloc(int64_t size) {
    size += sizeof(alloc_block_t);  // Adjust for metadata size

    if (bytes_allocated > trigger_threshold) {
        gc_collect();
    }

    bytes_allocated += size;

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
        int64_t alloc_size = max(size, MINIMUM_BLOCK_SIZE);
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
    gc_mark_stack();
    gc_sweep();
    trigger_threshold = max(bytes_allocated * 2, MINIMUM_THRESHOLD);
    bytes_allocated = 0;
}

static void gc_mark_stack(void) {
    mark_count = 0;

    void** current = get_sp();
    void** end = (void**)stack_start;

    while (current < end) {
        gc_mark(*current);
        current++;
    }
}

/* Mark a single object and recursively mark its contents */
static void gc_mark(void* ptr) {
    if (ptr == NULL) {
        return;
    }

    alloc_block_t* block = alloc_list_head;
    while (block != NULL) {
        void* block_data = (void*)(block + 1);
        if (block_data == ptr) {
            if (block->mark == 0) {
                mark_count++;
                block->mark = 1;

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

static void gc_sweep(void) {
    alloc_block_t* current = alloc_list_head;
    alloc_block_t* prev = NULL;

    while (current != NULL) {
        if (current->mark == 0) {
            alloc_block_t* next = current->next;

            if (prev == NULL) {
                alloc_list_head = next;
            } else {
                prev->next = next;
            }

            bytes_allocated -= (current->size + sizeof(alloc_block_t));

            free_block_t* free_block = (free_block_t*)current;
            free_block->size = current->size + sizeof(alloc_block_t) - sizeof(free_block_t);
            free_block->next = NULL;

            insert_into_free(free_block);

            current = next;
        } else {
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

static void merge(free_block_t* block) {
    while (block->next != NULL) {
        char* block_end = (char*)block + block->size + sizeof(free_block_t);
        if (block_end == (char*)block->next) {
            free_list_size--;
            block->size += block->next->size + sizeof(free_block_t);
            block->next = block->next->next;
        } else {
            break;
        }
    }
}

static void* sys_mmap(size_t size) {
    void* result = mmap(NULL, size, PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

    if (result == MAP_FAILED) {
        perror("[sys_mmap] mmap failed");
        exit(1);
    }

    return result;
}

static int64_t max(int64_t a, int64_t b) {
    if (a > b) {
        return a;
    } else {
        return b;
    }
}

void* get_sp(void) {
    volatile unsigned long var = 0;
#pragma GCC diagnostic push
#pragma GCC diagnostic ignored "-Wreturn-local-addr"
    return (void*)((unsigned long)&var + 4);
#pragma GCC diagnostic pop
}