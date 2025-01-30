section .bss
    buffer resb 20

section .text
    global itoa

itoa:
    push rbx
    push rsi
    push rdx
    mov rax, rdi
    mov rsi, buffer + 19
    mov byte [rsi], 0
    dec rsi
.loop:
    xor rdx, rdx
    mov rbx, 10
    div rbx
    add dl, '0'
    mov [rsi], dl
    dec rsi
    test rax, rax
    jnz .loop
    inc rsi
    mov rax, rsi
    pop rdx
    pop rsi
    pop rbx
    ret
