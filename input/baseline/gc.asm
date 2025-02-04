global gc_init, gc_alloc
extern alloc, free, printint, printstr, endl

section .bss
	alloc_list_head:	resq 1
	free_list_head:		resq 1
	stack_start:		resq 1

section .data
	gc_bytes_allocated:		dq 0				; bytes allocated since the last gc cycle
	gc_trigger_threshold:	dq 1024 * 1024 * 8	; initial gc trigger threshold in bytes (adjusts dynamically)
	gc_start_text:			db "Running gc after ", 0
	gc_sweep_done_text:		db "    Sweep done. We no have ", 0
	gc_next_threshold:		db "    The next threshold is ", 0
	gc_allocated_bytes:		db " allocated bytes", 0
	gc_mark_done_text:		db "    Marking done", 0

section .text
gc_init:
	mov [stack_start], rsp
	ret

gc_alloc:
	add rdi, 24
	mov rdx, [gc_bytes_allocated]
	cmp rdx, [gc_trigger_threshold]
	jb .skip_collect				; if allocated bytes since last collect has exceeded threshold, trigger collect
	push rdi
	call gc_collect
	pop rdi
.skip_collect:
	add [gc_bytes_allocated], rdi
	push rdi
	call alloc						; allocate size + metadata
	pop rdi
	mov byte [rax], 0				; set mark to 0
	mov qword [rax + 8], rdi		; set total size of object (including metadata)
	mov rsi, [alloc_list_head]		; load first item in allocation list
	mov qword [rax + 16], rsi		; make current head of allocation list the next item in this object
	mov [alloc_list_head], rax		; update head of allocation list so it points to this object
	add rax, 24						; skip metadata for return value
	ret

gc_collect:
	mov rdi, gc_start_text
	call printstr
	mov rdi, [gc_bytes_allocated]
	call printint
	mov rdi, gc_allocated_bytes
	call printstr
	call endl
	
	call gc_mark_stack
	
	mov rdi, gc_mark_done_text
	call printstr
	call endl
	
	call gc_sweep
	
	mov rdi, gc_sweep_done_text
	call printstr
	mov rdi, [gc_bytes_allocated]
	call printint
	mov rdi, gc_allocated_bytes
	call printstr
	call endl
	
	mov rdi, [gc_bytes_allocated]
	shl rdi, 1
	mov rsi, 1024 * 1024 * 8
	call max
	mov [gc_trigger_threshold], rax 
	mov qword [gc_bytes_allocated], 0
	
	mov rdi, gc_next_threshold
	call printstr
	mov rdi, [gc_trigger_threshold]
	call printint
	mov rdi, gc_allocated_bytes
	call printstr
	call endl
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
	mov rsi, [alloc_list_head]	; load start of allocation list
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
	mov rdi, [alloc_list_head]
	xor rsi, rsi
.loop:
	test rdi, rdi					; reached end of list?
	jz .done						; yes? return
	mov al, [rdi]
	test al, al						; is object marked?
	jz .unmarked					; no? free it
	mov byte [rdi], 0				; yes? clear mark for next marking
	mov rsi, rdi
	mov rdi, [rdi + 16]				; load the next object in the list
	jmp .loop						; repeat
.unmarked:
	mov rdx, [rdi + 16]				; save address of next object in list
	test rsi, rsi
	jz .remove_head
	mov [rsi + 16], rdx				; unlink the current node by setting the previous node's next to the next node's address
	jmp .free
.remove_head:
	mov [alloc_list_head], rdx			; update head node to be the next node
.free:
	push rsi						; save previous node since it will also be the previous node for the next item
	push rdx						; save next node
	mov rdx, [rdi + 8]				; load the size of the object
	sub [gc_bytes_allocated], rdx	; adjust allocated bytes
	call free						; free the memory
	pop rdi							; input for next iteration
	pop rsi							; prev node for next iteration
	jmp .loop
.done:
	ret
	
max:
	cmp rdi, rsi
	jae .left
	mov rax, rsi
	ret
.left:
	mov rax, rdi
	ret
