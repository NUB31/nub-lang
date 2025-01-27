global strlen
section .text

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