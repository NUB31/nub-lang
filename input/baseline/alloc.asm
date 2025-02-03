global alloc, free

section .bss
	free_list_head:		resq 1	; head of free list
	alloc_list_head:	resq 1	; head of allocation list

section .text
alloc:
	add rdi, 16					; reserve 16 bytes for metadata
	mov rsi, [free_list_head]
	xor r8, r8
.loop:
	test rsi, rsi				; allocate new block if end of list is reached
	jz .new_block
	mov rdx, [rsi]
	cmp rdx, rdi				; is there enough space for allocation?
	ja .use_block				; yes? allocate
	add rdx, 16
	cmp rdx, rdi				; no? is there enough space if we include metadata
	je .use_block				; if we include metadata, the sizes has to match exactly, or partial metadata will persist
	mov r8, rsi					; r8 contains the node from the last iteration
	mov rsi, [rsi + 8]			; next node	
	jmp .loop
.new_block:
	push rdi
	push r8
	add rdi, 16
	mov rsi, 4096
	call max
	push rax
	mov rdi, rax
	call sys_mmap
	pop rsi
	sub rsi, 16
	mov qword [rax], rsi		; update metadata to page size - metadata
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
.unlink_head:
	mov rdx, [free_list_head]	; load head
	mov rdx, [rdx + 8]			; load head.next
	mov [free_list_head], rdx	; mov head.next into head
.unlink_done:
	sub [rsi], rdi				; reduce available space of block by the allocated space
	mov rdx, [rsi]				; load the available space excluding the newly allocated space
	lea rax, [rsi + rdx + 16]	; load the address of the newly allocated space
	sub rdi, 16
	mov [rax], rdi				; update metadata to allocation size - metadata
	mov rdx, [alloc_list_head]
	mov [rax + 8], rdx			; move head to nex item in this alloc
	mov [alloc_list_head], rax	; update head to point to this node
	lea rax, [rax + 16]			; skip past metadata for return value
	ret

free:
	lea rdi, [rdi - 16]			; adjust for metadata
	mov rsi, [alloc_list_head]
	xor r8, r8
.loop:
	test rsi, rsi
	jz .not_found
	cmp rdi, rsi
	je .found
	mov r8, rsi
	mov rsi, [rsi + 8]			; next node
	jmp .loop
.not_found:
	mov rax, 60
	mov rdi, 1
	syscall
.found:
	test r8, r8					; r8 is null if node is also head
	jz .unlink_head
	mov rdx, [rsi + 8]			; load next node
	mov [r8 + 8], rdx			; link next node to last node's next
	jmp .unlink_done
.unlink_head:
	mov rdx, [alloc_list_head]	; load head
	mov rdx, [rdx + 8]			; load head.next
	mov [alloc_list_head], rdx	; mov head.next into head
.unlink_done:
	mov rdi, rsi
	call insert_into_free
	ret

insert_into_free:
	mov rsi, [free_list_head]	; load head
	test rsi, rsi				; is list empty
	jz .insert_head				; if empty, insert at head
	cmp rdi, rsi				; is input smaller then head
	jl .insert_head				; if smaller, insert at head
	xor r8, r8					; r9 will track the previous node
.loop:
	test rsi, rsi
	jz .insert_end				; if at end of list, insert at end
	cmp rdi, [rsi + 8]			; compare input to next node
	jg .next					; if larger, skip to next node
	mov [rsi + 8], rdi			; if smaller, insert at this position
	mov [rdi + 8], rdx
	ret
.next:
	mov r8, rsi					; update r8 to current node 
	mov rsi, [rsi + 8]			; update rsi to next node
	jmp .loop
.insert_head:
	mov rdx, [free_list_head]
	mov [rdi + 8], rdx
	mov [free_list_head], rdi
	ret
.insert_end:
	mov [r8 + 8], rdi			; update last node's next to point at rdi
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
