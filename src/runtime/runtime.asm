global _start
extern main

section .text
_start:
    ; Extract argc and argv from the stack
    mov rdi, [rsp]        ; rdi = argc
    lea rsi, [rsp + 8]    ; rsi = argv (pointer to array of strings)

    ; Call main(argc, argv)
    call main             ; main returns int in rax

    ; Exit with main's return value
    mov rdi, rax          ; exit code
    mov rax, 60           ; syscall: exit
    syscall

global nub_strcmp

nub_strcmp:
    xor rdx, rdx
.loop:
    mov al, [rsi + rdx]
    mov bl, [rdi + rdx]
    inc rdx
    cmp al, bl
    jne .not_equal
    cmp al, 0
    je .equal
    jmp .loop
.not_equal:
    mov rax, 0
    ret
.equal:
    mov rax, 1
    ret