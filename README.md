# Case prático: consumo de API, persistência e API de consulta de acordos

Fase 1 (ingestão e persistência): um produtor consulta a API pública (jsonplaceholder), publica mensagens em uma fila (SQS, simulado com LocalStack) e um consumidor grava os dados em um banco relacional (PostgreSQL), de forma idempotente. A API de consulta (Fase 3) lê apenas o banco.

Hierarquia respeitada: **Autor → Acordos → Documentos** (`users` → `albums` → `photos`).

## Arquitetura

```
 jsonplaceholder            LocalStack (SQS)                        PostgreSQL
 (API pública)     ┌─────────────────────────────┐
      ▲            │  acordos ──(3 falhas)──► acordos-dlq-consumo
      │ HTTPS      │     ▲                       │
 ┌────┴─────┐      │     │ publica    ┌──────────┴──┐   upsert    ┌─────────┐
 │ Ingestão │──────┼─────┘            │ Consumidor  │────────────►│ autor   │
 │ (produtor)│     │                  └─────────────┘ (1 transação)│ acordo  │
 └────┬─────┘      │  acordos-dlq-ingestao ◄── falha de uma       │documento│
      └────────────┼──────────────────── unidade da carga         └─────────┘
```

| Local (este repositório) | AWS (alvo) |
|---|---|
| Worker `CaseAcordos.Ingestao` (disparo manual ou timer de N horas) | EventBridge Scheduler (a cada 3h) + Lambda de Ingestão |
| LocalStack SQS: `acordos`, `acordos-dlq-consumo`, `acordos-dlq-ingestao` | SQS + DLQ de consumo (redrive) + DLQ de ingestão (destino On-Failure do Lambda) |
| Worker `CaseAcordos.Consumidor` | Lambda com trigger SQS (a mesma classe `ProcessadorMensagem`) |
| PostgreSQL no docker-compose | RDS PostgreSQL (acesso via RDS Proxy) |
| Logs JSON no console | Logs JSON enviados ao Datadog |

Para trocar o LocalStack pelo SQS real basta deixar `Filas:UrlServicoSqs` em branco: o código usa a cadeia padrão de credenciais da AWS e resolve a URL das filas por `GetQueueUrl`.

## Decisões principais

- **1 mensagem por acordo, com o autor embutido.** Cada mensagem é autocontida (autor + acordo + documentos). Não depende da ordem das demais, então a fila padrão basta (não precisa ser FIFO), vários consumidores gravam em paralelo e uma falha afeta um acordo só.
- **Idempotência por upsert.** `INSERT ... ON CONFLICT (id) DO UPDATE ... WHERE (valores atuais) IS DISTINCT FROM (novos)`, usando os ids da API pública como chave. Reentrega da fila e reexecução da carga não duplicam registros, e o `UPDATE` só ocorre se algum valor mudou.
- **Autor, acordo e documentos em uma única transação.** A FK nunca fica pendurada.
- **Tradução na borda.** Só a camada `Integracao` do produtor conhece os nomes do jsonplaceholder (`Tradutor`). Fila, banco e API usam autor/acordo/documento.
- **Retentativa seletiva na chamada à API pública.** Até 3 retentativas com backoff exponencial e jitter (~1s, 2s, 4s), somente para timeout, falha de rede, 5xx, 408 e 429. 400/404 não são repetidos.
- **Duas DLQs com significados diferentes.**
  - `acordos-dlq-ingestao`: cada unidade da carga que falhou (usuários, acordos de um usuário, documentos de um acordo, publicação), com etapa, ids, erro e se era retentável. A carga continua com o restante e o processo termina com código de saída 1.
  - `acordos-dlq-consumo`: mensagens que o consumidor não conseguiu gravar. Falha transitória (banco indisponível): a mensagem volta para a fila e, no 3º recebimento (`maxReceiveCount=3`, visibility timeout de 60s), o SQS a move para a DLQ. Falha não retentável (JSON inválido, dado rejeitado pelo banco): vai direto para a DLQ com o motivo no atributo `MotivoFalha`.
- **CorrelationId.** Gerado a cada carga, vai no corpo e no atributo da mensagem e aparece em todos os logs (produtor e consumidor).
- **Logs estruturados em JSON** (início, fim, quantidade processada e falhas da carga).

## Como executar

Veja o `GUIA_PASSO_A_PASSO.md` (criação da solução do zero, execução, verificação e cenários de falha). Resumo:

```bash
docker compose up -d                                   # Postgres + LocalStack + filas
dotnet run --project src/CaseAcordos.Consumidor        # terminal 1
dotnet run --project src/CaseAcordos.Ingestao          # terminal 2 (executa uma carga e encerra)
dotnet test
```

## Configuração

| Chave | Padrão | Descrição |
|---|---|---|
| `JsonPlaceholder:UrlBase` | `https://jsonplaceholder.typicode.com/` | API pública |
| `Ingestao:GrauParalelismo` | `1` | usuários processados ao mesmo tempo |
| `Ingestao:IntervaloHoras` | `0` | `0` = uma carga e encerra; `3` = repete a cada 3h |
| `Retentativa:Maximo` / `AtrasoBase` / `JitterMaximo` / `TimeoutPorTentativa` | `3` / `1s` / `500ms` / `10s` | política de retry |
| `Filas:UrlServicoSqs` | vazio (AWS) / `http://localhost:4566` em Development | endpoint do LocalStack |
| `Filas:FilaAcordos` / `FilaDlqConsumo` / `FilaDlqIngestao` | `acordos` / `acordos-dlq-consumo` / `acordos-dlq-ingestao` | nomes das filas |
| `Consumidor:GrauParalelismo` | `4` | mensagens do lote gravadas ao mesmo tempo |
| `ConnectionStrings:Acordos` | (obrigatória no consumidor) | conexão com o PostgreSQL |

Qualquer chave pode ser sobrescrita por variável de ambiente com `__` (ex.: `Filas__UrlServicoSqs`). Não há segredos no código: as credenciais de `appsettings.Development.json` e do compose são valores fictícios de ambiente local.

## Testes

`dotnet test` executa os testes unitários (xUnit): tradução e de-para, política de retentativa e classificação de falhas, cliente HTTP (503, 429, 404, timeout), serviço de ingestão (carga completa, falhas parciais, DLQ) e processador de mensagens do consumidor (validação, falhas retentáveis e não retentáveis).

## Próximos passos

### Limitação conhecida: identidade dos autores

Hoje, o `users.id` do jsonplaceholder é persistido diretamente como PK de `autor`, e o upsert usa esse identificador. Isso atende ao contrato deste case e funciona com a fonte de dados fixa utilizada aqui, mas significa que a identidade do autor depende da estabilidade do identificador externo. Se a origem reutilizasse o mesmo ID para outra pessoa, o upsert poderia atualizar o autor existente e associar os dados à identidade errada.

Como evolução para uma integração com uma fonte real, o banco poderia ter uma chave própria para `autor` (por exemplo, um UUID) e armazenar separadamente a origem e o ID externo, com unicidade em `(origem, id_externo)`. Em um cenário corporativo, a identidade deveria ser vinculada ao cadastro mestre da organização, quando disponível. Um serviço de identidade consultado durante a ingestão só seria necessário se essa resolução fosse responsabilidade de outro sistema; ele não elimina o risco de reutilização do ID externo sem uma fonte confiável para confirmar a identidade.

Essa evolução não faz parte deste case. Ela exigiria uma migração do schema e dos dados, além de ajustar o upsert do autor, a resolução da chave interna usada pela FK de `acordo`, as consultas e os testes. O contrato externo poderia continuar usando o ID do jsonplaceholder — por exemplo, em `GET /api/v1/usuarios/{id}/acordos` — desde que a consulta traduzisse esse ID para a chave interna. Portanto, não é uma troca isolada de coluna, mas também não exige necessariamente mudar o contrato da API.

Fase 2: consultas SQL. Fase 3: API de consulta (`GET /api/v1/usuarios/{id}/acordos`). Diferenciais (paginação de documentos e acordos, reprocessamento da DLQ, etc.) depois do obrigatório.
