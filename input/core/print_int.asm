section .bss
    buffer resb 20  ; Reserve 20 bytes for number string

section .text
    global print_int  ; Make function callable from outside

print_int:
    push rbx         ; Save rbx (callee-saved register)
    push rsi         ; Save rsi (callee-saved register)
    push rdx         ; Save rdx (callee-saved register)

    mov rax, rdi     ; Move input number to rax
    mov rsi, buffer + 19  ; Point to the last byte in buffer
    mov byte [rsi], 0    ; Null terminator (not necessary for sys_write)
    dec rsi               ; Move back for digits

.loop:
    xor rdx, rdx         ; Clear remainder
    mov rbx, 10          ; Divisor
    div rbx              ; RAX / 10 -> Quotient in RAX, remainder in RDX

    add dl, '0'          ; Convert remainder to ASCII
    mov [rsi], dl        ; Store character in buffer
    dec rsi              ; Move buffer pointer back

    test rax, rax        ; Check if quotient is 0
    jnz .loop        ; Continue if not 0

    inc rsi              ; Adjust pointer to first digit

    ; Print using sys_write
    mov rax, 1           ; syscall: sys_write
    mov rdi, 1           ; file descriptor: stdout
    mov rdx, buffer + 20 ; End of buffer
    sub rdx, rsi         ; Compute actual length
    syscall

    ; Restore registers and return
    pop rdx
    pop rsi
    pop rbx
    ret
