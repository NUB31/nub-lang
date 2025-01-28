global strlen
global strcmp
section .text

strcmp:
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

strlen:
    xor rax, rax
.loop:
    cmp byte [rdi], 0
    jz .done
    inc rax
    inc rdi
    jmp .loop
.done:
    ret