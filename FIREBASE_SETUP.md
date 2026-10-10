# Configuração do Firebase (login + chat + sincronização)

O app usa o Firebase por **REST** (sem SDK nativo, sem `google-services.json`).
As 3 chaves já estão em `Services/FirebaseConfig.cs` (projeto `babyantonio`).

## O que precisa estar ativo no console
1. **Authentication → Sign-in method → Email/Password → Enable.**
2. **Realtime Database** criado.

## Regras de segurança (Realtime Database → Rules)
Cole **estas regras** (cobrem chat, usuários e a sincronização por família) e **Publish**:
```json
{
  "rules": {
    "chat": {
      "global": {
        "messages": {
          ".read": "auth != null",
          ".write": "auth != null"
        }
      }
    },
    "users": {
      "$uid": {
        ".read": "auth != null && auth.uid === $uid",
        ".write": "auth != null && auth.uid === $uid"
      }
    },
    "families": {
      "$fid": {
        ".read": "auth != null",
        ".write": "auth != null"
      }
    }
  }
}
```
Sem essas regras, o login funciona mas a **sincronização dos dados do bebê é bloqueada** (e o app não mostra erro — só não sincroniza).

## Como usar a sincronização entre dois celulares
1. Nos dois celulares: abra o app → **💬 Chat** → crie a conta de cada um (um e-mail para você, outro para sua esposa) e defina o apelido.
2. No **seu** celular: **Config. → 👨‍👩‍👦 Família → "Criar família"**. Anote o código (ex.: `ABC123`).
3. No celular **dela**: **Config. → 👨‍👩‍👦 Família → "Entrar com código"** → digite o código → **Entrar**.
4. Pronto: mamadas, fraldas, sono, água, conquistas e vitaminas passam a aparecer nos dois. Puxe a tela do Painel para baixo para atualizar na hora.

> Observação: o check de "vitamina tomada hoje" ainda é por celular (sincronizo depois, se quiser).

---
## Opcional: bot com IA de verdade
O assistente do chat responde por regras (grátis, offline). Para um bot com IA,
seria preciso uma **Cloud Function** (plano **Blaze**, com cartão) guardando a
chave da IA no servidor.
