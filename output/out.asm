global _start
extern strlen

section .bss
    label1: resq 1
    label2: resq 1
    label3: resq 1

section .text
_start:
    mov rax, 1
    mov [label1], rax
    mov rax, 1
    mov [label2], rax
    mov rax, 2
    mov [label3], rax
    call label4
    mov rdi, 0
    mov rax, 60
    syscall

label4:
    push rbp
    mov rbp, rsp
    sub rsp, 0
    mov rax, label16
    push rax
    pop rdi
    call label12
    mov rax, 1
    push rax
    pop rdi
    call label14
    mov rax, label19
    push rax
    pop rdi
    call strlen
    push rax
    mov rax, 1
    mov rbx, rax
    pop rax
    cmp rax, rax
    sete al
    movzx rax, al
    cmp rax, 0
    je label18
    mov rax, label20
    push rax
    pop rdi
    call label12
    jmp label17
label18:
    mov rax, 0
    cmp rax, 0
    je label21
    mov rax, label22
    push rax
    pop rdi
    call label12
    jmp label17
label21:
    mov rax, 1
    cmp rax, 0
    je label23
    mov rax, label24
    push rax
    pop rdi
    call label12
    jmp label17
label23:
    mov rax, label25
    push rax
    pop rdi
    call label12
label17:
label5:
    mov rsp, rbp
    pop rbp
    ret

label6:
    push rbp
    mov rbp, rsp
    sub rsp, 8
    mov [rbp - 8], rdi
    mov rax, [label1]
    push rax
    mov rax, [label2]
    push rax
    mov rax, [rbp - 8]
    push rax
    mov rax, [rbp - 8]
    push rax
    pop rdi
    call strlen
    push rax
    pop rdx
    pop rsi
    pop rdi
    pop rax
    syscall
label7:
    mov rsp, rbp
    pop rbp
    ret

label8:
    push rbp
    mov rbp, rsp
    sub rsp, 8
    mov [rbp - 8], rdi
    mov rax, [rbp - 8]
    cmp rax, 0
    je label27
    mov rax, label28
    push rax
    pop rdi
    call label6
    jmp label26
label27:
    mov rax, label29
    push rax
    pop rdi
    call label6
label26:
label9:
    mov rsp, rbp
    pop rbp
    ret

label10:
    push rbp
    mov rbp, rsp
    sub rsp, 0
    mov rax, label30
    push rax
    pop rdi
    call label6
label11:
    mov rsp, rbp
    pop rbp
    ret

label12:
    push rbp
    mov rbp, rsp
    sub rsp, 8
    mov [rbp - 8], rdi
    mov rax, [rbp - 8]
    push rax
    pop rdi
    call label6
    call label10
label13:
    mov rsp, rbp
    pop rbp
    ret

label14:
    push rbp
    mov rbp, rsp
    sub rsp, 8
    mov [rbp - 8], rdi
    mov rax, [rbp - 8]
    push rax
    pop rdi
    call label8
    call label10
label15:
    mov rsp, rbp
    pop rbp
    ret

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

section .data
label16: db `test`, 0
label19: db `1`, 0
label20: db `1`, 0
label22: db `2`, 0
label24: db `3`, 0
label25: db `4`, 0
label28: db `true`, 0
label29: db `false`, 0
label30: db `\n`, 0
