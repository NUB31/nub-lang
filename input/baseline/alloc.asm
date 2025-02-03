global alloc, free

section .bss
	free_list_head:		resq 1	; head of free list
	alloc_list_head:	resq 1	; head of allocation list

section .text
alloc:
	add rdi, 16					; add space for metadata
	mov rax, [free_list_head]	; load head of free list
	xor r8, r8					; last block
.loop:
	test rax, rax				; end of list?
	jz .new_block				; yes? allocate new block
	cmp [rax], rdi				; does object fit in block and have space for metadata?
	jge .found_block			; yes? use this block
	mov r8, rax
	mov rax, [rax + 8]			; load next free block
	jb .loop					; no? go to next block
.found_block:
	sub qword [rax], rdi		; reduce the available size of the block
	cmp qword [rax], 0			; no space left in block?
	jg .done_remove_free_block	; no? do not remove block from free list
	mov rsi, [rax + 8]			; yes? remove block from free list
	test r8, r8					; is current head of list?
	jz .remove_free_head		; yes? remove head
	mov [r8 + 8], rsi			; set prev.next to this.next
	jmp .done_remove_free_block
.remove_free_head:
	mov [free_list_head], rsi
.done_remove_free_block:
	mov rsi, [rax]				; load size of block excluding the newly allocated object
	lea rax, [rax + rsi + 16]	; address of allocated block
	sub rdi, 16
	mov [rax], rdi				; save size
	mov rsi, [alloc_list_head]	; load head of allocated blocks
	mov [rax + 8], rsi			; move head to be next item after this block
	mov [alloc_list_head], rax	; set new head to this block
	lea rax, [rax + 16]			; skip metadata for return value
	ret
.new_block:
	push rdi
	push r8
	mov rdi, 4096				; page size
	call sys_mmap				; allocate a page
	mov qword [rax], 4080		; set size of block to block size - metadata
	mov rsi, [free_list_head]
	mov qword [rax + 8], rsi	; move head to be the next item after this block
	mov [free_list_head], rax	; set new head to this block
	pop r8
	pop rdi
	jmp .found_block

free:
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
