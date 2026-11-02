# Guia passo a passo: Fase 1 (do `dotnet new` até rodar)

Pré-requisitos: **.NET 10 SDK**, **Docker Desktop** e **Git**. Os comandos funcionam no PowerShell e no bash, salvo onde indicado.

O arquivo `case_acordos_fase1.zip` contém os **fontes** (arquivos `.cs`, `appsettings`, `docker-compose`, SQL, Dockerfiles, README). Ele **não** contém `.csproj` nem `.sln`: você os cria com os comandos abaixo, para a solução nascer do zero na sua máquina.

---

## 1. Criar a solução

```bash
git clone https://github.com/anabiax/case_acordos.git
cd case_acordos

dotnet new gitignore
dotnet new sln -n CaseAcordos          # no .NET 10 gera CaseAcordos.slnx (formato novo; funciona igual)

dotnet new classlib -n CaseAcordos.Contratos    -o src/CaseAcordos.Contratos    -f net10.0
dotnet new classlib -n CaseAcordos.Mensageria   -o src/CaseAcordos.Mensageria   -f net10.0
dotnet new classlib -n CaseAcordos.Persistencia -o src/CaseAcordos.Persistencia -f net10.0
dotnet new worker   -n CaseAcordos.Ingestao     -o src/CaseAcordos.Ingestao     -f net10.0
dotnet new worker   -n CaseAcordos.Consumidor   -o src/CaseAcordos.Consumidor   -f net10.0
dotnet new xunit    -n CaseAcordos.Testes       -o tests/CaseAcordos.Testes     -f net10.0
```

## 2. Adicionar os projetos à solução

```bash
dotnet sln add src/CaseAcordos.Contratos/CaseAcordos.Contratos.csproj
dotnet sln add src/CaseAcordos.Mensageria/CaseAcordos.Mensageria.csproj
dotnet sln add src/CaseAcordos.Persistencia/CaseAcordos.Persistencia.csproj
dotnet sln add src/CaseAcordos.Ingestao/CaseAcordos.Ingestao.csproj
dotnet sln add src/CaseAcordos.Consumidor/CaseAcordos.Consumidor.csproj
dotnet sln add tests/CaseAcordos.Testes/CaseAcordos.Testes.csproj
```

## 3. Referências entre projetos

```bash
dotnet add src/CaseAcordos.Persistencia reference src/CaseAcordos.Contratos

dotnet add src/CaseAcordos.Ingestao   reference src/CaseAcordos.Contratos src/CaseAcordos.Mensageria
dotnet add src/CaseAcordos.Consumidor reference src/CaseAcordos.Contratos src/CaseAcordos.Mensageria src/CaseAcordos.Persistencia

dotnet add tests/CaseAcordos.Testes reference src/CaseAcordos.Contratos src/CaseAcordos.Persistencia src/CaseAcordos.Ingestao src/CaseAcordos.Consumidor
```

## 4. Pacotes NuGet

```bash
dotnet add src/CaseAcordos.Mensageria   package AWSSDK.SQS --version "3.7.*"
dotnet add src/CaseAcordos.Persistencia package Npgsql
dotnet add src/CaseAcordos.Persistencia package Dapper
dotnet add src/CaseAcordos.Ingestao     package Microsoft.Extensions.Http
```

> `AWSSDK.SQS` fica na série 3.7 (a que usei como referência). O `Consumidor` e a `Ingestao` recebem o pacote de forma transitiva pela referência ao projeto `Mensageria`, e o `Consumidor` recebe `Npgsql` e `Dapper` pela referência a `Persistencia`.

## 5. Remover os arquivos de exemplo dos templates

PowerShell:
```powershell
Remove-Item src/CaseAcordos.Contratos/Class1.cs, src/CaseAcordos.Mensageria/Class1.cs, src/CaseAcordos.Persistencia/Class1.cs
Remove-Item src/CaseAcordos.Ingestao/Worker.cs, src/CaseAcordos.Consumidor/Worker.cs
Remove-Item tests/CaseAcordos.Testes/UnitTest1.cs
```
bash:
```bash
rm src/CaseAcordos.Contratos/Class1.cs src/CaseAcordos.Mensageria/Class1.cs src/CaseAcordos.Persistencia/Class1.cs
rm src/CaseAcordos.Ingestao/Worker.cs src/CaseAcordos.Consumidor/Worker.cs
rm tests/CaseAcordos.Testes/UnitTest1.cs
```

## 6. Copiar os fontes do zip

Extraia `case_acordos_fase1.zip` e copie o conteúdo **por cima** da pasta do repositório (mesclando as pastas `src`, `tests`, `db` e `docker`). Isso **substitui** o `Program.cs` e os `appsettings*.json` gerados pelos templates dos dois workers, que é o esperado. Não precisa apagar nem alterar nenhum `.csproj`.

Ao final, na raiz do repositório devem existir: `docker-compose.yml`, `.dockerignore`, `.env.example`, `README.md`, `db/01_schema.sql`, `docker/Dockerfile.*`, `src/...` e `tests/...`.

## 7. Compilar e rodar os testes

```bash
dotnet build
dotnet test
```

Esperado: build sem erros e **44 testes** passando. Se aparecer erro de compilação, me envie a mensagem completa.

## 8. Subir a infraestrutura

```bash
docker compose up -d
docker compose ps
docker compose logs sqs-init
```

- `postgres` e `localstack` devem ficar `healthy`.
- `sqs-init` roda e **termina** (é normal ele aparecer como `exited (0)`); o log deve listar as 3 filas: `acordos`, `acordos-dlq-consumo`, `acordos-dlq-ingestao`.
- O Postgres aplica o `db/01_schema.sql` sozinho na primeira subida.

> **LocalStack:** a tag `latest` passou a exigir conta/token (desde 23/03/2026). O compose fixa `localstack/localstack:4.12` (versão comunitária). Se o container reclamar de token, copie `.env.example` para `.env` e siga as instruções de lá.

## 9. Executar a carga

Terminal 1, consumidor (fica rodando):
```bash
dotnet run --project src/CaseAcordos.Consumidor
```
Terminal 2, ingestão (executa uma carga e encerra):
```bash
dotnet run --project src/CaseAcordos.Ingestao
```

Ambos usam `appsettings.Development.json` (LocalStack em `localhost:4566`, Postgres em `localhost:5432`). Para **depurar**, abra a solução no Visual Studio/Rider, defina os dois workers como projetos de inicialização e coloque breakpoints (por exemplo em `ServicoIngestao.ProcessarAcordoAsync` e `ProcessadorMensagem.ProcessarAsync`).

Nos logs (JSON) procure `Carga iniciada`, `Acordo publicado`, `Acordo gravado` e `Carga finalizada`, todos com o mesmo `CorrelationId`.

## 10. Conferir o resultado no banco

```bash
docker compose exec postgres psql -U postgres -d acordos -c "select (select count(*) from autor) autores, (select count(*) from acordo) acordos, (select count(*) from documento) documentos;"
```

Pelo que conheço do jsonplaceholder, o esperado é **10 autores, 100 acordos e 5000 documentos** (confirme com o que a API devolver). Rode a ingestão **de novo**: os totais não podem mudar, o que comprova a idempotência.

## 11. Cenários de falha para a demonstração

Defina uma função `aws` (AWS CLI dentro da rede do compose) e use-a nos comandos abaixo.

PowerShell:
```powershell
function aws { docker run --rm --network case-acordos_default -e AWS_ACCESS_KEY_ID=test -e AWS_SECRET_ACCESS_KEY=test -e AWS_DEFAULT_REGION=us-east-1 amazon/aws-cli --endpoint-url http://localstack:4566 @args }
```
bash:
```bash
aws() { docker run --rm --network case-acordos_default -e AWS_ACCESS_KEY_ID=test -e AWS_SECRET_ACCESS_KEY=test -e AWS_DEFAULT_REGION=us-east-1 amazon/aws-cli --endpoint-url http://localstack:4566 "$@"; }
```

**a) Mensagem inválida (não retentável) → DLQ de consumo**
```bash
aws sqs send-message --queue-url http://localstack:4566/000000000000/acordos --message-body "isto nao e json"
aws sqs receive-message --queue-url http://localstack:4566/000000000000/acordos-dlq-consumo --message-attribute-names All
```
O consumidor registra o erro e a mensagem aparece na DLQ com o atributo `MotivoFalha`.

**b) API pública indisponível → retentativas com backoff → DLQ de ingestão**
```powershell
$env:JsonPlaceholder__UrlBase = "http://localhost:9/"; dotnet run --project src/CaseAcordos.Ingestao
```
```bash
JsonPlaceholder__UrlBase=http://localhost:9/ dotnet run --project src/CaseAcordos.Ingestao
```
Nos logs: 3 avisos de retentativa (≈1s, 2s, 4s), depois `Falha na ingestão` com `Etapa=usuarios`; o processo termina com código de saída 1. Veja a falha na `acordos-dlq-ingestao` (`receive-message` como acima). Depois limpe a variável de ambiente.

**c) Erro de negócio da API pública (404) → sem retentativa**
```powershell
$env:JsonPlaceholder__UrlBase = "https://jsonplaceholder.typicode.com/naoexiste/"; dotnet run --project src/CaseAcordos.Ingestao
```
Falha imediata, **sem** avisos de retentativa, e a mensagem na DLQ de ingestão vem com `"retentavel": false`.

**d) Banco indisponível (falha transitória) → reentrega → DLQ de consumo**
```bash
docker compose stop postgres
dotnet run --project src/CaseAcordos.Ingestao        # publica as mensagens normalmente
```
O consumidor loga `Falha retentável` e não apaga as mensagens. A cada ~60s (visibility timeout) elas são reentregues e, no **3º recebimento**, o SQS as move para `acordos-dlq-consumo`. Para voltar ao normal: `docker compose start postgres` e rode a ingestão de novo (idempotente) para reprocessar.

## 12. Subir o código

```bash
git add .
git commit -m "Fase 1: ingestão e persistência (produtor, fila, consumidor, schema, testes)"
git push
```

## Limites do que foi validado antes de entregar

- A lógica central foi compilada com o SDK do .NET 10 e os **44 testes foram executados** com sucesso. Como o ambiente em que trabalhei não acessa o NuGet, a verificação usou *stubs* mínimos no lugar de `AWSSDK.SQS`, `Npgsql` e `Dapper`.
- O **SQL do upsert** foi executado em um PostgreSQL 16 real: a 1ª gravação insere, repetir os mesmos dados não altera nada e uma mudança atualiza.
- **Não foi possível** rodar o fluxo ponta a ponta (LocalStack, Docker e jsonplaceholder estão fora do alcance daquele ambiente). Os pontos de maior risco são as classes finas que falam com as bibliotecas reais: `FabricaSqs`, `ResolvedorUrlFila`, `PublicadorSqs`, `ConsumidorSqs` e `RepositorioAcordosPostgres`. Se algo falhar ao compilar ou ao subir, mande a mensagem de erro.
