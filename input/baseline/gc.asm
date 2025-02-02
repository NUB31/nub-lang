global gc_init, gc_alloc

section .bss
    alloc_list: resq 1
    stack_start: resq 1
    total_alloc: resq 1

section .data
    gc_threshold: dq 4096       ; default of 4096 bytes, this will scale when gc_collect is ran
    
section .text
gc_init:
    mov [stack_start], rsp
    ret

gc_alloc:
    add rdi, 17                 ; add space for metadata
    mov rdx, [total_alloc]      ; load total allocation
    cmp rdx, [gc_threshold]     ; has total exceeded threshold?
    jb .no_collect              ; no? skip
    push rdi
    call gc_collect
    pop rdi
.no_collect:
    add [total_alloc], rdi      ; save total allocation
    push rdi
    call sys_mmap               ; allocate size + metadata
    pop rdi
    mov byte [rax], 0           ; set mark to 0
    mov qword [rax + 1], rdi    ; set total size of object (including metadata)
    mov rsi, [alloc_list]       ; load first item in allocation list
    mov qword [rax + 9], rsi    ; make current head of allocation list the next item in this object
    mov [alloc_list], rax       ; update head of allocation list so it points to this object
    add rax, 17                 ; skip metadata for return value
    ret

gc_collect:
    call gc_mark_stack
    call gc_sweep
    mov rdi, [total_alloc]          ; since we just swept, all the memory is in use
    shl rdi, 1                      ; double the currently used memory
    mov rsi, 4096
    call max                        ; get the largest of total_alloc * 2 and 4096
    mov qword [gc_threshold], rax   ; update threshold to new value
    ret
    
gc_mark_stack:    
    mov r8, rsp             ; load current stack pointer
    mov r9, [stack_start]   ; load start of stack
.loop:
    cmp r8, r9              ; have we reached end of stack?
    ja .done                ; yes? return
    mov rdi, [r8]           ; no? load the value
    call gc_mark            ; this might be an allocation, check
    add r8, 8               ; next item in stack
    jmp .loop
.done:
    ret
    
gc_mark:
    test rdi, rdi           ; is input null?
    jz .done                ; yes? return
    mov rsi, [alloc_list]   ; load start of allocation list
.loop:
    test rsi, rsi           ; reached end of list?
    jz .done                ; yes? return
    lea rdx, [rsi + 17]
    cmp rdx, rdi            ; no? is this the input object?
    je .mark_object         ; yes? mark it
    mov rsi, [rsi + 9]      ; no? next item
    jmp .loop
.mark_object:
    mov al, [rdi]           ; load mark
    test al, al             ; already marked?
    jnz .done               ; yes? return
    mov byte [rdi - 17], 1  ; mark object
    mov rcx, [rdi + 1]      ; load object size
    mov rdx, rdi            ; start of data
    add rcx, rdx            ; end of data
.scan_object:
    cmp rdx, rcx            ; done scanning?
    jae .done               ; yes? return
    mov rdi, [rbx]          ; load value
    call gc_mark
    add rdx, 8              ; next object
    jmp .scan_object
.done:
    ret
    
gc_sweep:
    mov rdi, [alloc_list]
    xor rsi, rsi
.loop:
    test rdi, rdi           ; reached end of list?
    jz .done                ; yes? return
    mov al, [rdi]
    test al, al             ; is object marked?
    jz .free                ; no? free it
    mov byte [rdi], 0       ; yes? clear mark for next marking
    mov rsi, rdi
    mov rdi, [rdi + 9]      ; load the next object in the list
    jmp .loop               ; repeat
.free:
    mov rdx, [rdi + 9]      ; save address of next object in list
    test rsi, rsi
    jz .remove_head
    mov [rsi + 9], rdx      ; unlink the current node by setting the previous node's next to the next node's address
    jmp .free_memory
.remove_head:
    mov [alloc_list], rdx   ; update head node to be the next node
.free_memory
    push rsi                ; save previous node since it will also be the previous node for the next item
    push rdx                ; save next node
    mov rsi, [rdi + 1]      ; get length of the object
    sub [total_alloc], rsi  ; remove this allocation from total allocations
    call sys_munmap         ; free the memory
    pop rdi                 ; input for next iteration
    pop rsi                 ; prev node for next iteration
    jmp .loop
.done:
    ret
    
sys_mmap:
    mov rax, 9
    mov rsi, rdi
    mov rdi, 0
    mov rdx, 3
    mov r10, 34
    mov r8, -1
    mov r9, 0
    syscall
    cmp rax, -1
    je .error
    ret
.error:
    mov rax, 60
    mov rdi, 1
    syscall
    
sys_munmap:
    mov rax, 11
    syscall
    cmp rax, -1
    je .error
    ret
.error:
    mov rax, 60
    mov rdi, 1
    syscall
    
max:
    cmp rdi, rsi
    jae .left
    mov rax, rsi
    ret
.left:
    mov rax, rdi
    ret