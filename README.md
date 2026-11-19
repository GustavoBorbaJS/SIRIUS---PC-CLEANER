<div align="center">

# 🛡️ Sirius Cleaner

**Limpador de lixo e ferramenta de manutenção para Windows que não quebra o que está rodando.**

Feito para quem usa o PC para trabalhar: serviços .NET, IIS, builds, bancos de dados e IDEs continuam intactos enquanto o lixo vai embora.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6)
![WinForms](https://img.shields.io/badge/UI-WinForms-informational)

</div>

---

## Sumário

- [Por que mais um limpador?](#por-que-mais-um-limpador)
- [Recursos](#recursos)
- [Como a limpeza protege seus serviços](#como-a-limpeza-protege-seus-serviços)
- [O que é limpo](#o-que-é-limpo)
- [Manutenção do Windows](#manutenção-do-windows)
- [Instalação e compilação](#instalação-e-compilação)
- [Uso](#uso)
- [Linha de comando e agendamento](#linha-de-comando-e-agendamento)
- [Configurações](#configurações)
- [Estrutura do projeto](#estrutura-do-projeto)
- [Testes](#testes)
- [O que o Sirius deliberadamente NÃO faz](#o-que-o-sirius-deliberadamente-não-faz)
- [Solução de problemas](#solução-de-problemas)

---

## Por que mais um limpador?

Limpadores comuns apagam `%TEMP%` inteiro, matam processos "ociosos" e esvaziam pastas que runtimes usam em segundo plano. Para quem desenvolve ou hospeda aplicações isso causa erros difíceis de rastrear:

| Sintoma | Causa típica |
|---|---|
| App .NET single-file para de funcionar depois de uma "limpeza" | `%TEMP%\.net\` (onde o runtime extrai as DLLs) foi apagada |
| Build falha com arquivo não encontrado | `MSBuildTemp*` ou `VBCSCompiler` removidos no meio do build |
| API local "caiu sozinha" | o limpador matou o `dotnet.exe` por estar com CPU baixa |
| Restore do NuGet corrompido | `NuGetScratch` apagado durante o restore |

O Sirius foi reescrito para que **nada disso aconteça**.

## Recursos

- 🧹 **Limpeza em 21 categorias** — temporários, logs, relatórios de erro, Windows Update, caches de navegadores e apps, NuGet, Lixeira.
- 🔍 **Analisar antes de limpar** — mostra itens e tamanho por categoria sem apagar nada.
- 🛡️ **Travas de segurança em camadas** (detalhes abaixo) — arquivos em uso, recentes e pastas de runtime são sempre preservados.
- ⏱️ **Limpeza automática inteligente** — só quando o PC está ocioso, respeita bateria e intervalo mínimo.
- 🔧 **Manutenção do Windows** — DISM, SFC, limpeza do WinSxS, otimização de unidades, DNS, ponto de restauração.
- 📊 **Monitor de processos e RAM** — classifica cada processo (Sistema, Serviço, .NET, Dev, Aplicativo) com consumo leve.
- 🖥️ **Bandeja do sistema**, inicialização com o Windows, instância única e **modo linha de comando** para o Agendador de Tarefas.
- 📝 **Log com rotação** em `%LOCALAPPDATA%\SiriusCleaner\sirius.log`.

## Como a limpeza protege seus serviços

Cada arquivo só é apagado se passar por **todas** as travas abaixo. Se qualquer uma falhar, ele é preservado e contado como "preservado" no relatório.

1. **Nunca segue links** — junções e links simbólicos são ignorados; a limpeza nunca sai da pasta que está limpando.
2. **Idade mínima** — arquivos criados *ou* modificados há menos de 24 h (configurável) ficam. Usa a data de criação e de modificação, não a de último acesso (que o Windows quase não atualiza).
3. **Pastas de runtime e ferramentas protegidas** — qualquer pasta ou arquivo cujo nome comece com:
   `.net`, `dotnet`, `VBCSCompiler`, `MSBuild`, `MSBuildTemp`, `NuGet`, `NuGetScratch`, `ASP.NET`, `Temporary ASP.NET Files`, `IISExpress`, `IIS Temporary Compressed Files`, `vsdbg`, `VSRemoteDebugger`, `JetBrains`.
   Arquivos `*.lock`, `*.lck`, `*.pid` e `*.sock` também ficam.
4. **Teste de arquivo em uso** — antes de apagar, o Sirius tenta abrir o arquivo de forma exclusiva. Se um serviço tiver um handle aberto (log sendo escrito, DLL carregada), o arquivo fica.
5. **Programa dono aberto = categoria pulada** — o cache do Chrome só é limpo com o Chrome fechado, o do NuGet só sem `dotnet`/`MSBuild`/IDE abertos, o do Windows Update só com o serviço parado.
6. **Raízes perigosas bloqueadas** — mesmo com variável de ambiente corrompida, o Sirius se recusa a limpar recursivamente `C:\`, `C:\Windows`, `Program Files`, `ProgramData`, o perfil do usuário, `AppData`, Área de Trabalho ou Documentos.
7. **Pastas vazias** só são removidas se forem antigas, e a remoção falha naturalmente se a pasta for o diretório de trabalho de algum processo.
8. **Suas próprias exceções** — liste pastas em *Configurações → Pastas protegidas*.

### E os processos?

**O Sirius nunca encerra processos sozinho.** As antigas opções "Limpar inativos", "Limpar não-sistema" e "Auto-limpeza de processos" foram removidas: um serviço ocioso é exatamente um processo com CPU e memória baixas.

Na aba *Processos* você ainda pode encerrar um programa manualmente, mas:

- **Sistema**, **Serviços** (tudo na sessão 0 ou que hospede um serviço do Windows — IIS/`w3wp`, Kestrel como serviço, SQL Server…) e **processos protegidos** por você são **bloqueados**;
- processos **.NET** (`dotnet`, `iisexpress`, `MSBuild`, `VBCSCompiler`, `devenv`, `testhost`… ou qualquer processo que tenha carregado `coreclr.dll`/`clr.dll`) e de **desenvolvimento** (`node`, `sqlservr`, `docker`, `wsl`…) exigem uma confirmação explícita;
- o PID é revalidado pela hora de início antes de encerrar, evitando matar outro processo que reutilizou o mesmo PID.

## O que é limpo

| Grupo | Categoria | Padrão | Observações |
|---|---|:---:|---|
| Windows | Temporários do usuário (`%TEMP%`) | ✅ | pastas .NET/MSBuild/NuGet/IIS preservadas |
| Windows | Temporários do Windows (`C:\Windows\Temp`) | ✅ | 🔒 admin · idade mínima 72 h |
| Windows | Relatórios de erro (WER) | ✅ | parte do usuário sem admin, parte do sistema com admin |
| Windows | Cache de Internet (INetCache) | ✅ | |
| Windows | Logs antigos (CBS, DISM, Windows Update) | ✅ | 🔒 admin · mais de 7 dias |
| Windows | Downloads do Windows Update | ⬜ | 🔒 admin · mais de 10 dias · pulado com `wuauserv`/`BITS` ativos |
| Windows | Cache de miniaturas | ⬜ | recriado sob demanda |
| Windows | Cache de shaders (DirectX/NVIDIA/AMD/Intel) | ⬜ | jogos podem engasgar na 1ª execução |
| Windows | Crash dumps | ⬜ | úteis para depurar apps .NET — marque só se não precisar |
| Navegadores | Chrome, Edge, Brave, Opera, Firefox | ✅ | só cache; histórico, senhas, cookies e abas **não** são tocados |
| Aplicativos | VS Code, Discord, Slack, Teams clássico | ✅ | só cache e logs |
| Desenvolvimento | Cache HTTP do NuGet (`v3-cache`, `plugins-cache`) | ⬜ | `~/.nuget/packages` **nunca** é tocado |
| Desenvolvimento | Logs do Visual Studio | ⬜ | mais de 7 dias |
| Pessoais | Lixeira | ⬜ | pede confirmação · nunca entra na limpeza automática |

🔒 = requer executar como administrador. Sem admin essas categorias são puladas (com aviso), e o resto funciona normalmente.

> **Prefetch não é limpo de propósito.** Apagar `C:\Windows\Prefetch` deixa o boot e a abertura de programas *mais lentos*; o Windows já gerencia essa pasta sozinho.

## Manutenção do Windows

Aba *Manutenção* — ferramentas oficiais do Windows, com a saída exibida no painel de atividade. Nenhuma encerra programas em uso.

| Tarefa | Comando executado | Admin |
|---|---|:---:|
| Criar ponto de restauração | `Checkpoint-Computer` | 🔒 |
| Limpar cache DNS | `ipconfig /flushdns` | |
| Limpar componentes do Windows (WinSxS) | `DISM /Online /Cleanup-Image /StartComponentCleanup` | 🔒 |
| Limpar cache de Otimização de Entrega | `Delete-DeliveryOptimizationCache -Force` | 🔒 |
| Reparar imagem do Windows | `DISM /Online /Cleanup-Image /RestoreHealth` | 🔒 |
| Verificar arquivos do sistema | `sfc /scannow` | 🔒 |
| Otimizar unidades (TRIM/desfragmentar) | `defrag /C /O /U` | 🔒 |

Ordem sugerida para um PC com problemas: **ponto de restauração → DISM RestoreHealth → SFC → limpar componentes**.

## Instalação e compilação

### Requisitos

- Windows 10 ou 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) para executar
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (ou superior) para compilar

### Compilar e executar

```powershell
git clone https://github.com/GustavoBorbaJS/SIRIUS---PC-CLEANER.git
cd SIRIUS---PC-CLEANER
dotnet run --project SiriusCleaner.csproj
```

### Gerar um executável único

```powershell
dotnet publish SiriusCleaner.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

O resultado é `publish\SiriusCleaner.exe` (~1 MB, precisa do .NET 8 Desktop Runtime instalado). Para um executável que roda sem o runtime instalado, troque para `--self-contained true` (≈ 150 MB).

## Uso

1. Abra o **Sirius Cleaner**. Para limpar áreas do sistema, clique em **Executar como administrador** no topo.
2. Na aba **Limpeza**, marque as categorias desejadas (as recomendadas já vêm marcadas).
3. Clique em **Analisar** para ver quanto espaço cada categoria libera — nada é apagado.
4. Clique em **Limpar**. A coluna *Situação* mostra o que foi limpo e o que foi preservado (e por quê).

Fechar a janela mantém o Sirius na bandeja (configurável). Pelo ícone da bandeja há **Limpeza rápida** e **Sair**.

## Linha de comando e agendamento

```text
SiriusCleaner.exe             Abre a interface
SiriusCleaner.exe --tray      Inicia minimizado na bandeja
SiriusCleaner.exe --analyze   Mostra o que seria limpo, sem apagar nada
SiriusCleaner.exe --clean     Limpa as categorias marcadas na interface e sai
```

`--clean` e `--analyze` usam as mesmas categorias e travas da interface (a Lixeira nunca entra) e gravam o resultado no log. Exemplo para agendar uma limpeza semanal como administrador:

```powershell
schtasks /Create /TN "Sirius Cleaner" /TR "\"C:\Ferramentas\SiriusCleaner.exe\" --clean" /SC WEEKLY /D SUN /ST 03:00 /RL HIGHEST
```

## Configurações

Aba *Configurações*, salvas em `%LOCALAPPDATA%\SiriusCleaner\settings.json`:

| Opção | Padrão | Descrição |
|---|---|---|
| Idade mínima dos arquivos | 24 h | arquivos mais novos nunca são apagados (algumas categorias usam um mínimo maior) |
| Processos protegidos | — | nomes que nunca podem ser encerrados, um por linha |
| Pastas protegidas | — | caminhos que a limpeza nunca toca; aceita `%VARIAVEIS%` |
| Limpeza automática | desligada | roda só categorias seguras para segundo plano |
| Intervalo | 240 min | tempo mínimo entre duas limpezas automáticas |
| Ociosidade | 10 min | só limpa se o usuário não mexer no PC por esse tempo |
| Usar bateria | não | em notebooks, só limpa na tomada |
| Iniciar com o Windows | não | registra em `HKCU\...\Run` com `--tray` |
| Fechar mantém na bandeja | sim | |

## Estrutura do projeto

```text
SiriusCleaner.csproj / .sln
Program.cs                 Ponto de entrada: instância única, linha de comando, tratamento de erros
Core/                      Infraestrutura
  AppInfo.cs               Nome, versão, pastas, detecção de administrador
  AppSettings.cs           Configurações (JSON, gravação atômica)
  Logger.cs                Log em arquivo com rotação
  NativeMethods.cs         P/Invoke (memória, ociosidade, Lixeira, caminho de processo)
  StartupManager.cs        Iniciar com o Windows
  SystemState.cs           RAM, ociosidade, bateria, formatação
Safety/                    Travas de segurança
  PathGuard.cs             Pastas protegidas e raízes proibidas
  ProcessGuard.cs          Classificação de processos (Sistema/Serviço/.NET/Dev/App)
  ServiceInfo.cs           Serviços do Windows ativos e seus PIDs
Cleaning/                  Motor de limpeza
  CleanupCatalog.cs        Todas as categorias e seus caminhos
  CleanupCategory.cs       Modelo de categoria/alvo
  CleanupReport.cs         Resultados por categoria
  JunkCleaner.cs           Varredura, verificações e exclusão
Processes/ProcessMonitor.cs  Lista de processos com CPU por diferença (sem sleeps)
Maintenance/MaintenanceTasks.cs  Catálogo e execução das ferramentas do Windows
UI/                        Interface WinForms (abas, gráfico de RAM, tema)
tests/SiriusCleaner.Tests  Testes automatizados das travas de segurança
```

### Adicionando uma categoria

Inclua um item em `Cleaning/CleanupCatalog.cs`:

```csharp
new CleanupCategory
{
    Id = "app-meuapp", Group = GroupApps, Name = "Cache do MeuApp",
    Description = "Cache local do MeuApp.",
    DefaultEnabled = true, AllowInAutoClean = true, MinAgeHours = 1,
    BlockingProcesses = new[] { "MeuApp" },           // pula se estiver aberto
    Targets = () => new[] { new CleanupTarget(Path.Combine(Local, @"MeuApp\Cache")) },
},
```

Todas as travas do `JunkCleaner` se aplicam automaticamente.

## Testes

```powershell
dotnet test SiriusCleaner.sln
```

Os testes rodam numa pasta temporária isolada e verificam, entre outros: arquivos antigos são apagados e recentes ficam; pastas `.net`, `MSBuildTemp`, `NuGetScratch`, `VBCSCompiler` e `Temporary ASP.NET Files` são preservadas; arquivos abertos por outro processo ficam; junções não são seguidas; a análise nunca apaga; categorias com o programa dono aberto são puladas; raízes perigosas são recusadas; processos .NET e serviços são classificados corretamente.

## O que o Sirius deliberadamente NÃO faz

- **Encerrar processos automaticamente** — nunca.
- **"Liberar RAM"** esvaziando o working set de outros processos — o ganho é ilusório e causa lentidão (e latência em serviços) logo depois.
- **Limpar o Prefetch** — piora o desempenho.
- **Limpar registro** — risco alto, benefício nulo em Windows modernos.
- **Apagar histórico, cookies ou senhas** dos navegadores.
- **Licenças, senhas ou período de teste** — o programa é livre para uso.

## Solução de problemas

| Problema | Solução |
|---|---|
| Itens com 🔒 aparecem como "Ignorada: requer administrador" | Clique em **Executar como administrador** |
| Categoria de navegador "Ignorada: chrome está aberto" | Feche o navegador (verifique a bandeja; o Edge pode ficar em segundo plano) |
| Muitos arquivos "em uso" no `%TEMP%` | Normal: são de programas abertos. Reinicie o PC e limpe de novo |
| Um app meu usa uma pasta em `%TEMP%` | Adicione a pasta em *Configurações → Pastas protegidas* |
| Quero ver o que aconteceu | *Configurações → Abrir pasta de logs* (`sirius.log`) |

Dados do aplicativo ficam em `%LOCALAPPDATA%\SiriusCleaner` (configurações e logs). Para remover tudo, apague essa pasta e desative *Iniciar com o Windows* antes.
