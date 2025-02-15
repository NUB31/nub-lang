global gc_init, gc_alloc
extern printint, printstr, endl

section .bss
	alloc_list_head:	resq 1	; metadata size: 24
	free_list_head:		resq 1	; metadata size: 16
	stack_start:		resq 1
	free_list_size:		resq 1
	mark_count:			resq 1

section .data
	gc_bytes_allocated:		dq 0								; bytes allocated since the last gc cycle
	gc_trigger_threshold:	dq 1024 * 1024 * 8					; initial gc trigger threshold in bytes (adjusts dynamically)
	txt_start_collect:		db "Running gc after ", 0
	txt_sweep_done:			db "    Sweep done. We now have ", 0
	txt_next_threshold:		db "    The next threshold is ", 0
	txt_allocated_bytes:	db " allocated bytes", 0
	txt_marking_done:		db "    Marking done. Objects marked is ", 0
	txt_free_list_size:		db "    Free list size is ", 0

section .text
gc_init:
	mov [stack_start], rsp
	ret

gc_alloc:
	add rdi, 24						; adjust for metadata size
	mov rdx, [gc_bytes_allocated]
	cmp rdx, [gc_trigger_threshold]
	jbe .alloc						; if allocated bytes since last collect has exceeded threshold, trigger collect
	push rdi
	call gc_collect
	pop rdi
.alloc:
	add [gc_bytes_allocated], rdi	; adjust allocated bytes list
	mov rsi, [free_list_head]
	xor r8, r8
.loop:
	test rsi, rsi
	jz .new_block					; allocate new block if at end of list
	mov rdx, [rsi]
	cmp rdi, rdx
	jbe .use_block					; use block if object fits within block
	mov r8, rsi						; load r8 with current node
	mov rsi, [rsi + 8]				; load next node
	jmp .loop
.new_block:
	push rdi
	push r8
	mov rsi, 4096
	call max						; calculate size of allocation (max(input, 4096))
	mov rdi, rax
	push rdi
	call sys_mmap
	pop rsi
	sub rsi, 16
	mov qword [rax], rsi			; set size of object to page size - metadata
	mov qword [rax + 8], 0			; ensure that next pointer is null
	push rax
	mov rdi, rax
	call insert_into_free
	pop rsi
	pop r8
	pop rdi
.use_block:
	cmp [rsi], rdi				; check if the block will be empty after allocation
	ja .unlink_done				; if not, do not unlink
	test r8, r8					; r8 is null if node is also head
	jz .unlink_head
	mov rdx, [rsi + 8]			; load next node
	mov [r8 + 8], rdx			; link next node to last node's next
	jmp .unlink_done
	dec qword [free_list_size]
.unlink_head:
	mov rdx, [free_list_head]	; load head
	mov rdx, [rdx + 8]			; load head.next
	mov [free_list_head], rdx	; mov head.next into head
	dec qword [free_list_size]
.unlink_done:
	sub [rsi], rdi				; reduce available space of block by the allocated space
	mov rdx, [rsi]				; load the available space excluding the newly allocated space
	lea rax, [rsi + rdx + 16]	; load the address of the newly allocated space
	mov byte [rax], 0			; update mark to 0
	sub rdi, 24
	mov [rax + 8], rdi			; update metadata size to allocation size - metadata
	mov rdx, [alloc_list_head]
	mov [rax + 16], rdx
	mov [alloc_list_head], rax	; append this allocation to the head of allocation list
	lea rax, [rax + 24]			; skip past metadata for return value
	ret

gc_collect:
	mov rdi, txt_start_collect
	call printstr
	mov rdi, [gc_bytes_allocated]
	call printint
	mov rdi, txt_allocated_bytes
	call printstr
	call endl
	
	call gc_mark_stack
	call gc_sweep
	
	mov rdi, txt_sweep_done
	call printstr
	mov rdi, [gc_bytes_allocated]
	call printint
	mov rdi, txt_allocated_bytes
	call printstr
	call endl
	
	mov rdi, [gc_bytes_allocated]
	shl rdi, 1
	mov rsi, 1024 * 1024 * 8
	call max
	mov [gc_trigger_threshold], rax 
	mov qword [gc_bytes_allocated], 0
	
	mov rdi, txt_next_threshold
	call printstr
	mov rdi, [gc_trigger_threshold]
	call printint
	mov rdi, txt_allocated_bytes
	call printstr
	call endl
	
	mov rdi, txt_free_list_size
	call printstr
	mov rdi, [free_list_size]
	call printint
	call endl
	ret

gc_mark_stack:
	mov qword [mark_count], 0
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
	mov rdi, txt_marking_done
	call printstr
	mov rdi, [mark_count]
	call printint
	call endl
	ret

gc_mark:
	test rdi, rdi
	jz .done					; if stack item is null, return
	mov rsi, [alloc_list_head]
.loop:
	test rsi, rsi
	jz .done					; return if end of list is reached
	lea rdx, [rsi + 24]			; input value does not include metadata
	cmp rdx, rdi
	je .mark_object				; if match is found, mark the object
	mov rsi, [rsi + 16]			; load next item and repeat
	jmp .loop
.mark_object:
	inc qword [mark_count]
	mov al, [rdi]				; load mark
	test al, al					; already marked?
	jnz .done					; yes? return
	mov byte [rdi - 24], 1		; mark object
	mov rcx, [rdi - 16]			; load object size
	lea rcx, [rdi + rcx]		; end of object
.scan_object:
	cmp rdi, rcx
	jge .done
	push rdi
	mov rdi, [rdi]
	call gc_mark
	pop rdi
	lea rdi, [rdi + 8]
	jmp .scan_object
.done:
	ret

gc_sweep:
	mov rdi, [alloc_list_head]
	xor r8, r8
.loop:
	test rdi, rdi					; reached end of list?
	jz .done						; yes? return
	mov al, [rdi]
	test al, al
	jz .unmarked					; if unmarked, free object
	mov byte [rdi], 0				; unmark object
	mov r8, rdi
	mov rdi, [rdi + 16]				; next item
	jmp .loop
.done:
	ret
.unmarked:
	mov r9, [rdi + 16]				; save address of next object in list
	test r8, r8
	jz .unlink_head					; if current is head, unlink head
	mov [r8 + 16], r9				; unlink the current node by setting the previous node's next to the next node's address
	jmp .unlink_done
.unlink_head:
	mov [alloc_list_head], r9		; update head node to be the next node
.unlink_done:
	push r8							; save previous node since it will also be the previous node for the next item
	push r9							; save next node
	mov rdx, [rdi + 8]				; load current size
	add rdx, 24						; add metadata size back
	sub [gc_bytes_allocated], rdx	; adjust allocated bytes
	mov rdx, [rdi + 8]				; load current size
	add rdx, 8						; adjust for smaller metadata in free list
	mov [rdi], rdx					; save new size in correct position
	mov qword [rdi + 8], 0				; set next to null
	call insert_into_free
	pop rdi							; input for next iteration
	pop r8							; prev node for next iteration
	jmp .loop

insert_into_free:
	mov rsi, [free_list_head]	; rsi will track the current node
	test rsi, rsi
	jz .insert_head				; if list is empty, insert at head
	cmp rdi, rsi
	jb .insert_head				; is input is smaller than head, insert at head
.loop:
	mov r9, [rsi + 8]			; load next node
	test r9, r9
	jz .insert_tail				; if at end of the list, insert at tail
	cmp rdi, r9
	jbe .insert_between			; if input < next insert between current and next
	mov rsi, r9
	jmp .loop
.insert_between:
	mov [rdi + 8], r9			; input.next = next
	mov [rsi + 8], rdi			; current.next = input
	inc qword [free_list_size]
	mov rdi, rsi
	call merge
	ret
.insert_head:
	mov [rdi + 8], rsi
	mov [free_list_head], rdi
	inc qword [free_list_size]
	call merge
	ret
.insert_tail:
	mov qword [rdi + 8], 0		; set input.tail to null
	mov [rsi + 8], rdi			; add input to current.next
	inc qword [free_list_size]
	mov rdi, rsi
	call merge
	ret
	
merge:
	mov rsi, [rdi + 8]
	test rsi, rsi
	jz .return
	mov rdx, [rdi]
	lea rdx, [rdi + rdx + 16]
	cmp rdx, rsi
	jne .return	
	dec qword [free_list_size]
	mov rdx, [rsi]
	add rdx, 16
	add [rdi], rdx
	mov rdx, [rsi + 8]
	mov [rdi + 8], rdx
	jmp merge
.return:
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
