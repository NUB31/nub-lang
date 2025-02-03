global gc_init, gc_alloc
extern alloc, free

section .bss
	alloc_list:			resq 1		; head of alloc list
	stack_start:		resq 1		; start of stack

section .data
	gc_threshold_c:		dq 1024		; default of 1024 allocations
	total_alloc_c:		dq 0		; count the amount of allocations

section .text
gc_init:
	mov [stack_start], rsp
	ret

gc_alloc:
	add rdi, 24					; add space for metadata
	mov rdx, [total_alloc_c]	; load total allocation count
	cmp rdx, [gc_threshold_c]	; has count exceeded threshold?
	jb .skip_collect			; yes? run gc
	push rdi
	call gc_collect
	pop rdi
.skip_collect:
	inc qword [total_alloc_c]	; update total allocation count
	push rdi
	call alloc					; allocate size + metadata
	pop rdi
	mov byte [rax], 0			; set mark to 0
	mov qword [rax + 8], rdi	; set total size of object (including metadata)
	mov rsi, [alloc_list]		; load first item in allocation list
	mov qword [rax + 16], rsi	; make current head of allocation list the next item in this object
	mov [alloc_list], rax		; update head of allocation list so it points to this object
	add rax, 24					; skip metadata for return value
	ret

gc_collect:
	call gc_mark_stack
	call gc_sweep
	mov qword [total_alloc_c], 0	; reset allocation count
	ret

gc_mark_stack:	
	mov r8, rsp				; load current stack pointer
	mov r9, [stack_start]	; load start of stack
.loop:
	cmp r8, r9				; have we reached end of stack?
	jae .done				; yes? return
	mov rdi, [r8]			; no? load the value
	call gc_mark			; this might be an allocation, check
	lea r8, [r8 + 8]		; next item in stack
	jmp .loop
.done:
	ret

gc_mark:
	test rdi, rdi			; is input null?
	jz .done				; yes? return
	mov rsi, [alloc_list]	; load start of allocation list
.loop:
	test rsi, rsi			; reached end of list?
	jz .done				; yes? return
	lea rdx, [rsi + 24]
	cmp rdx, rdi			; no? is this the input object?
	je .mark_object			; yes? mark it
	mov rsi, [rsi + 16]		; no? next item
	jmp .loop
.mark_object:
	mov al, [rdi]			; load mark
	test al, al				; already marked?
	jnz .done				; yes? return
	mov byte [rdi - 24], 1	; mark object
	mov rcx, [rdi + 8]		; load object size
	mov rdx, rdi			; start of data
	add rcx, rdx			; end of data
.scan_object:
	cmp rdx, rcx			; done scanning?
	jae .done				; yes? return
	mov rdi, [rdx]			; load value
	call gc_mark
	add rdx, 8				; next object
	jmp .scan_object
.done:
	ret

gc_sweep:
	mov rdi, [alloc_list]
	xor rsi, rsi
.loop:
	test rdi, rdi			; reached end of list?
	jz .done				; yes? return
	mov al, [rdi]
	test al, al				; is object marked?
	jz .free				; no? free it
	mov byte [rdi], 0		; yes? clear mark for next marking
	mov rsi, rdi
	mov rdi, [rdi + 16]		; load the next object in the list
	jmp .loop				; repeat
.free:
	mov rdx, [rdi + 16]		; save address of next object in list
	test rsi, rsi
	jz .remove_head
	mov [rsi + 16], rdx		; unlink the current node by setting the previous node's next to the next node's address
	jmp .free_memory
.remove_head:
	mov [alloc_list], rdx	; update head node to be the next node
.free_memory:
	push rsi				; save previous node since it will also be the previous node for the next item
	push rdx				; save next node
	call free				; free the memory
	pop rdi					; input for next iteration
	pop rsi					; prev node for next iteration
	jmp .loop
.done:
	ret