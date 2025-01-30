section .bss
    buffer resb 20

section .text
    global itoa

itoa:
    mov rax, rdi
    mov rsi, buffer + 19
    mov byte [rsi], 0
    dec rsi
.loop:
    xor rdx, rdx
    mov rcx, 10
    div rcx
    add dl, '0'
    mov [rsi], dl
    dec rsi
    test rax, rax
    jnz .loop
    inc rsi
    mov rax, rsi
    ret
