global gc_init, gc_alloc

section .bss
    alloc_list: resq 1
    stack_start: resq 1
    total_alloc: resq 1

section .data
    gc_threshold: dq 4096                   ; default of 4096 bytes, this will scale when gc_collect is ran
    
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

; Generated by chatgpt. Rewrite this later
; TODO: refactor to unlink easier
gc_free:
    mov rsi, [alloc_list]        ; Load head of allocation list
    test rsi, rsi                ; Check if list is empty
    jz .not_found                ; If empty, nothing to free
    cmp rsi, rdi                 ; Is the first item the one to free?
    je .remove_head              ; If so, update head directly
.loop:
    mov rdx, [rsi + 9]           ; Load next item in list
    test rdx, rdx                ; Check if end of list
    jz .not_found                ; If not found, return
    cmp rdx, rdi                 ; Is this the item to remove?
    je .remove_item              ; If so, unlink it
    mov rsi, rdx                 ; Move to next item
    jmp .loop                    ; Repeat
.remove_head:
    mov rdx, [rdi + 9]           ; Get next item
    mov [alloc_list], rdx        ; Update head of list
    jmp .free_memory             ; Free the object
.remove_item:
    mov rdx, [rdi + 9]           ; Get next item
    mov [rsi + 9], rdx           ; Bypass rdi in the list
.free_memory:
    mov rsi, [rdi + 1]           ; Get object size
    sub [total_alloc], rsi       ; save total allocation
    call sys_munmap              ; Free memory
    ret
.not_found:
    ret                          ; Item not found, do nothing
    
gc_collect:
    call gc_mark_stack
    call gc_sweep
    ; next threshold will be double of used memory or 4096, whichever is higher
    mov rdi, [total_alloc]
    shl rdi, 1
    mov rsi, 4096
    call max
    mov qword [gc_threshold], rax
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
.loop:
    test rdi, rdi       ; reached end of list?
    jz .done            ; yes? return
    mov al, [rdi]
    test al, al         ; is object marked?
    jz .free            ; no? free it
    mov byte [rdi], 0   ; yes? clear mark for next scan
    mov rdi, [rdi + 9]  ; load the next object in the list
    jmp .loop           ; repeat
.free:
    mov rcx, [rdi + 9]  ; save address of next object in list
    push rcx
    call gc_free
    pop rdi             ; [rdi + 9] is deallocated now, and would throw a segfault unless we used the stack
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