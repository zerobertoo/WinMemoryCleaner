# Auditoria de i18n

Referência: `src/Resources/Localization/English.json` (73 chaves, espelhadas
nas 73 propriedades de `src/Model/Localization.cs`).

## Resultado

Os 29 idiomas restantes foram verificados e estão consistentes:

| Verificação | Resultado |
| --- | --- |
| JSON inválido | nenhum |
| Chaves faltantes | nenhuma |
| Chaves órfãs (fora do English) | nenhuma |
| Chaves duplicadas | nenhuma |
| Valores vazios | nenhum |
| Placeholders (`{0}`...) divergentes | nenhum |
| Encoding/EOL | UTF-8 sem BOM, CRLF, sem terminador final (igual em todos) |

Nenhuma tradução precisou ser preenchida nem caiu para inglês.

Valores idênticos ao inglês (nomes próprios/termos universais, provavelmente
intencionais): Albanian 1, Dutch 3, German 2, Indonesian 5, Italian 2,
Portuguese (Brazil) 1, Portuguese (Portugal) 1, Spanish 3. Os demais idiomas: 0.

## Como o carregamento funciona

`Localizer.Load` lê `<EnglishName>.json` (arquivo ao lado do exe tem
prioridade, senão o recurso embutido `Resources.Localization.*`) e desserializa
com `DataContractJsonSerializer` em `Localization`. Chave faltante vira `null`
e o `Load` lança "Missing Values", então uma chave faltante quebra a troca de
idioma (cai para English).

## Teste

`src/Test/LocalizationKeysTests.cs` falha se algum idioma tiver chave
faltante/órfã/vazia, placeholder divergente do English, ou se as chaves do
English divergirem das propriedades de `Localization`.

## Constants

Removido (sem nenhuma referência em `src/`): `Constants.App.Defaults.Path`.

Mantido por ser usado apenas internamente: `EmbeddedResourcePath` (compõe
`LocalizationResourcePath` e `ThemesResourcePath`).

Removidos junto com a janela de doação: `Donation.*` e `Certificate.*`.

### Decisões (aplicadas)

- **Donation** (`BitcoinUri`, `EthereumUri`, `GitHubSponsorUri`, `KofiUri`):
  carteiras e links do autor original (Igor Mundstein), usados por
  `DonationWindow`. Manter envia doações ao upstream pelo app do fork.
  Recomendação: remover a janela/botão de doação inteiros, ou trocar pelos
  seus próprios links (ex.: GitHub Sponsors do fork). Não deixar as carteiras
  do upstream.
- **Certificate Thumbprint** (Release/Test): thumbprints do certificado de
  assinatura (SignPath) do upstream; zero referências no código, e o fork não
  assina. Recomendação: remover o bloco `Certificate` inteiro (morto e
  enganoso).
- **Registry.Key** (`SOFTWARE\WinMemoryCleaner`, `...\ProcessExclusionList`):
  usado por `Settings` e `Migrator`. É o nome do hive, não identidade do
  upstream. Recomendação: manter; renomear faria o usuário perder as
  configurações salvas e exigiria migração, sem ganho.
