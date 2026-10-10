# Configuração do Firebase (login + chat)

O app usa o Firebase por **REST** (sem SDK nativo, sem `google-services.json`).
Só é preciso preencher 3 chaves públicas em `Services/FirebaseConfig.cs`.

## 1. Criar o projeto
1. Acesse https://console.firebase.google.com e crie um projeto (plano **Spark**, grátis, sem cartão).

## 2. Ativar login por e-mail/senha
1. **Build → Authentication → Get started**.
2. Aba **Sign-in method → Email/Password → Enable → Save**.

## 3. Criar o Realtime Database
1. **Build → Realtime Database → Create Database**.
2. Escolha a localização e comece em **test mode** (depois aplique as regras abaixo).
3. Anote a URL no topo, algo como:
   `https://SEU-PROJETO-default-rtdb.firebaseio.com`

## 4. Pegar as chaves
Em **⚙️ Project settings → General**:
- **Web API Key** (ex.: `AIza...`)
- **Project ID**

## 5. Preencher no app
Edite `Services/FirebaseConfig.cs`:
```csharp
public const string ApiKey      = "AIza...";                 // Web API Key
public const string ProjectId   = "seu-projeto";             // Project ID
public const string DatabaseUrl = "https://seu-projeto-default-rtdb.firebaseio.com";
```

## 6. Regras de segurança (Realtime Database → Rules)
Só usuários logados leem/escrevem o chat:
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
    }
  }
}
```
Publique as regras (**Publish**).

Pronto. Faça o build, instale, toque em **💬 Chat** no Painel, crie a conta e escolha o apelido.

---
## Opcional: bot com IA de verdade
O assistente atual responde por regras (grátis, offline). Para um bot que
responde qualquer pergunta com IA, seria preciso uma **Cloud Function** (plano
**Blaze**, com cartão) guardando a chave da IA no servidor — a chave nunca pode
ficar no app. Peça ao desenvolvedor se quiser esse upgrade.
