global _start
extern main, gc_init

section .text
_start:
    call gc_init
    call main
    mov rax, 60
    mov rdi, 0
    syscall