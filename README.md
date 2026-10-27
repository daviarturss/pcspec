# PcSpec

Clone funcional inspirado no **CPU-Z**, escrito em C#/.NET 10 e usando apenas
APIs públicas do Windows. Não usa driver de kernel, não lê a memória de outro
processo e não reutiliza código do CPU-Z: as informações vêm de **CPUID**,
**SMBIOS**, **WMI**, **registro** e APIs de topologia/energia do próprio sistema.

O projeto nasceu de um estudo de engenharia reversa do CPU-Z 3.0.1 (dossiê em
[`../cpuz-clone/docs/01-arquitetura.md`](../cpuz-clone/docs/01-arquitetura.md))
e cobre o que é possível obter sem instalar um driver assinado.

## O que ele mostra

| Área | Conteúdo |
| --- | --- |
| Sistema | Fabricante, modelo, família, SKU, série, UUID, chassi, SO, build, arquitetura, data de instalação, memória total, modo de boot (UEFI/legacy) e Secure Boot |
| BIOS | Fornecedor, versão, data, versão SMBIOS, tipo de firmware, tamanho da ROM e versão do BIOS do sistema |
| Placa-mãe | Fabricante, modelo, revisão, série, asset tag, tipo, chipset e southbridge; lista das pontes PCI |
| CPU | Nome, codename, socket, litografia, família/modelo/stepping, núcleos/threads/soquetes, clocks, multiplicador, tensão, instruções e caches (nível, tipo, tamanho, associatividade, linha e compartilhamento) |
| Memória | Total, capacidade máxima, slots ocupados, canais, tipo, velocidade, formato, ECC e um cartão por módulo SMBIOS (banco, tamanho, tipo, formato, velocidade, fabricante, part number, série, ranks, tensão e largura) |
| Gráficos | Adaptadores de vídeo via WMI: nome, fabricante, IDs PCI, processador, memória, driver, data, resolução, taxa de atualização e status |
| Benchmark | Quatro cargas (inteiro, ponto flutuante, banda de memória e SHA-256) em 1 thread e em N threads, com score normalizado e escalabilidade |
| Relatório | TXT e HTML autocontido (CSS embutido, sem recursos externos) e JSON completo |

## Estrutura

```
pcspec/
  src/
    PcSpec.Core/   Coleta e modelos (sem dependência de UI)
    PcSpec.Cli/    Interface de linha de comando
    PcSpec.App/    Aplicativo WPF (tema escuro, 7 abas)
  PcSpec.slnx
```

O `Core` é compartilhado pelo CLI e pelo app: a mesma coleta alimenta a tela, os
relatórios e o JSON.

## Requisitos

- Windows 10 19041+ ou Windows 11, x64.
- .NET 10 SDK para compilar; runtime .NET 10 para executar a publicação
  dependente de framework.
- Não é necessário executar como administrador. Algumas leituras (por exemplo,
  detalhes de firmware) podem variar conforme a política da máquina.

## Compilar e executar

```powershell
# build
dotnet build PcSpec.slnx

# CLI (relatório completo no console)
dotnet run --project src/PcSpec.Cli -- info

# app WPF
dotnet run --project src/PcSpec.App
```

## CLI

```
pcspec <comando> [opções]

Comandos
  info      Relatório completo em texto (padrão)
  system    Sistema operacional, chassi e memória total
  cpu       Processador: nome, família, caches, instruções, clocks
  board     Placa-mãe, BIOS, chipset e pontes PCI
  memory    Módulos de memória (SMBIOS)
  gpu       Adaptadores de vídeo
  bench     Benchmark single/multi-thread
  report    Gera relatório em arquivo (TXT ou HTML)
  json      Despeja tudo em JSON
  smbios    Lista as estruturas SMBIOS lidas do firmware
  help      Ajuda
```

Opções:

```
-o, --out <arquivo>      Caminho de saída (report/json)
-f, --format <txt|html>  Formato do relatório
    --open               Abre o relatório ao terminar
    --raw                Inclui bytes crus no comando smbios
    --bench              Inclui o benchmark em report/json
    --runs <n>           Repetições do benchmark (padrão 3)
    --duration <ms>      Duração por subteste (padrão 400)
-t, --threads <n>        Threads no benchmark multi (padrão: todos)
    --no-pin             Não fixa threads em núcleos
```

Exemplos:

```powershell
pcspec cpu
pcspec memory
pcspec bench --runs 5 --duration 800
pcspec report --format html --out pc.html --open --bench
pcspec json -o machine.json
pcspec smbios --raw
```

## App WPF

Sete abas (Sistema, CPU, Placa-mãe, Memória, Gráficos, Benchmark e Relatório),
com chips de resumo no topo e barra de status. Os botões permitem atualizar a
coleta, executar o benchmark, salvar TXT/HTML e copiar o relatório em texto.

## Fontes de dados

| Fonte | Uso |
| --- | --- |
| `CPUID` (`X86Base.CpuId`) | Fabricante, assinatura, brand string, instruções, caches (folha 4 ou `0x8000001D`), clocks nominais (`0x16`), P-states (`0x80000007`) |
| `GetSystemFirmwareTable('RSMB')` | Tabela SMBIOS bruta (tipos 0, 1, 2, 4, 16, 17, 19, 32) |
| `GetLogicalProcessorInformationEx` | Topologia (núcleos/threads/soquetes) e fallback de caches |
| `CallNtPowerInformation` | Frequência corrente por processador lógico |
| WMI (`System.Management`) | GPU, BIOS e dados de sistema que o SMBIOS não cobre |
| Registro do Windows | Chipset/pontes PCI (`Enum\PCI`), nome da CPU, modo de boot e Secure Boot |
| `GetNativeSystemInfo` / `RtlGetVersion` | Arquitetura e versão real do SO |

Todas as leituras são tolerantes a falha: se uma fonte não responder, o restante
do relatório continua e o problema aparece em **Diagnóstico**.

## Benchmark

Quatro cargas independentes, executadas em 1 thread e em N threads:

- **Inteiro**: laço de operações inteiras com dependência de dados.
- **Ponto flutuante**: quatro FMAs por iteração, em cadeias independentes
  (mede throughput, não latência).
- **Memória**: varredura sequencial de um buffer de 48 MB (banda em GB/s).
- **SHA-256**: hash de blocos de 4 MB com `SHA256` do .NET (GB/s).

Cada carga é calibrada para blocos de ~25 ms e medida pelo **próprio worker**
(uma barreira de largada sincroniza as threads). O resultado usa a melhor
repetição, o score single/multi é a média geométrica dos subtestes e a
escalabilidade é `multi / single`.

> O score do PcSpec **não é comparável** ao do CPU-Z: a carga e a normalização
> são próprias. Use-o para comparar máquinas entre si com a mesma versão.

## Limitações conhecidas

- **Timings de memória (CL/tRCD/tRP/tRAS) e perfis XMP/EXPO** exigem leitura SPD
  por SMBus, que no Windows requer um driver de kernel. O PcSpec mostra apenas o
  que o SMBIOS expõe.
- **Tensão do núcleo** e outros MSRs não são acessíveis sem driver; quando o
  SMBIOS não informa, o campo fica vazio.
- **Largura do barramento de memória da GPU** não é exposta pela WMI; o campo
  aparece como `—` quando o driver não informa.
- **Sensores de temperatura/ventoinha** não fazem parte do escopo; o projeto
  irmão [`hwmon`](../hwmon) cobre isso com LibreHardwareMonitorLib.
- O catálogo de pontes PCI é curado e cobre os chipsets mais comuns; IDs fora
  da tabela aparecem como `(não catalogada)`.
- Máquinas ARM64 não são suportadas (o CPUID é x86/x64).

## Publicação

```powershell
dotnet publish src/PcSpec.App/PcSpec.App.csproj -c Release -o dist/app
dotnet publish src/PcSpec.Cli/PcSpec.Cli.csproj -c Release -o dist/cli
```

Os executáveis publicados são `dist/app/PcSpec.exe` e `dist/cli/pcspec.exe`.

## Privacidade

Tudo é lido e processado localmente. O PcSpec não faz acesso à rede e não envia
dados para lugar nenhum.

## Licença

O código deste repositório é distribuído sob a licença **MIT**. A única
dependência de terceiros é `System.Management` (MIT, Microsoft).

CPU-Z é uma marca da CPUID. Este projeto não é afiliado à CPUID e foi escrito
de forma independente, a partir de APIs documentadas e de um estudo de
engenharia reversa do comportamento do programa original.
