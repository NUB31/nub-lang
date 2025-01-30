global str_len
section .text

str_len:
    xor rax, rax
.loop:
    cmp byte [rdi], 0
    jz .done
    inc rax
    inc rdi
    jmp .loop
.done:
    ret