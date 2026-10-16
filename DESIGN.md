---
name: WinMemoryCleaner
description: Janela única de otimização de memória do Windows, com leitura em mostrador e uma ação vermelha.
colors:
  signal-red: "#FF3B30"
  signal-red-light: "#E30613"
  signal-red-hover: "#E6352B"
  signal-red-pressed: "#CC2F26"
  ink-on-red: "#FFFFFF"
  night-ground: "#141414"
  night-card: "#1E1E1D"
  night-ink: "#F4F2EE"
  night-track: "#3A3A38"
  night-hairline: "#4A4A47"
  paper-ground: "#F4F2EE"
  paper-card: "#FBFAF8"
  paper-ink: "#111111"
  paper-track: "#D2CFC8"
  mute-border: "#8A8A85"
typography:
  readout:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "44px"
    fontWeight: 700
    lineHeight: 1
  action:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "28px"
    fontWeight: 700
  body:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "14px"
    fontWeight: 400
  card-title:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "12px"
    fontWeight: 600
rounded:
  card: "8px"
  square: "0px"
spacing:
  gutter: "5px"
  card-x: "14px"
  card-top: "10px"
  card-bottom: "12px"
  window-margin: "10px"
components:
  button-primary:
    backgroundColor: "{colors.signal-red}"
    textColor: "{colors.ink-on-red}"
    typography: "{typography.action}"
    rounded: "{rounded.card}"
    width: "230px"
  button-primary-hover:
    backgroundColor: "{colors.signal-red-hover}"
  button-primary-pressed:
    backgroundColor: "{colors.signal-red-pressed}"
  card:
    backgroundColor: "{colors.night-card}"
    textColor: "{colors.night-ink}"
    rounded: "{rounded.card}"
    padding: "10px 14px 12px"
---

# Design System: WinMemoryCleaner

## Overview

**Creative North Star: "The Station Clock"**

A utilidade lê como um relógio de estação ferroviária: uma leitura grande e exata, um ponteiro vermelho que diz o estado, e uma única ação vermelha. O resto são cartões calmos, em fundo Mica, que só entregam opções. A tela serve a uma tarefa de um clique e depois a leitura do resultado, então a hierarquia é: leitura, ação, configuração.

O sistema é construído sobre o tema Fluent do WPF-UI (controles nativos do Windows 11) e muda apenas três coisas: o vermelho de sinal como único destaque, a leitura em mostrador com numerais grandes, e seções em cartões. O visual é denso e sóbrio, pensado para um usuário técnico.

**Key Characteristics:**
- Um só destaque: vermelho de sinal, reservado a ação, estado e seleção.
- Leitura primeiro: mostrador de 72px e número de 44px em negrito.
- Cartões planos de 8px sobre Mica, sem sombras decorativas.
- Controles Fluent padrão (checkbox, combobox, slider) com o vermelho como cor de marcação.
- Modo compacto mantém apenas leitura e ação.

## Colors

Paleta de tinta e papel com um vermelho de sinal. As superfícies vêm do tema Fluent (claro e escuro); o vermelho é fixado por código para não ser clareado.

### Primary
- **Signal Red** (#FF3B30 no escuro, #E30613 no claro): botão Otimizar, ponteiro do mostrador, checkbox marcado, thumb e preenchimento de sliders, foco de combobox.
- **Signal Red Hover** (#E6352B) e **Pressed** (#CC2F26): estados do botão primário no tema escuro (no claro, #C9050F e #AF040D).

### Neutral
- **Night Ground** (#141414) e **Night Card** (#1E1E1D): fundo e cartões do tema escuro.
- **Night Ink** (#F4F2EE): texto e barras de "usado" no escuro.
- **Night Track** (#3A3A38): trilho das barras de memória no escuro.
- **Paper Ground** (#F4F2EE), **Paper Card** (#FBFAF8) e **Paper Ink** (#111111): equivalentes do tema claro.
- **Paper Track** (#D2CFC8): trilho das barras no claro.
- **Mute Border** (#8A8A85): contornos de controles e rótulos secundários.

### Named Rules
**The One Voice Rule.** O vermelho de sinal é a única cor de destaque; barras de memória e demais dados usam tinta e trilho neutros.
**The Fixed Red Rule.** O vermelho nunca é derivado da cor de destaque do sistema operacional; é fixado em `ThemeManager`.

## Typography

**Display, Body e Label:** Segoe UI Variable Text (com Segoe UI como reserva).

**Character:** uma família só, com contraste por peso e tamanho: negrito grande para a leitura e para a ação, peso normal para as opções.

### Hierarchy
- **Readout** (700, 44px, 1.0): valor de memória física em uso, ao lado do mostrador; a unidade vai em 18px negrito alinhada à linha de base.
- **Action** (700, 28px): texto do botão Otimizar.
- **Body** (400, 14px; ajustável de 12 a 18 pelo controle de fonte): opções, legendas "Usada | x GB (y%)".
- **Card Title** (600, 12px, cor secundária): título de cada cartão.

### Named Rules
**The Tabular Reading Rule.** Valores de memória são lidos em negrito e sempre com unidade ao lado; nunca em texto corrido.

## Layout

Janela única de tamanho fixo, que se ajusta ao conteúdo, com margem externa de 10px. De cima para baixo: barra de título; bloco de leitura (cartões de memória física e virtual à esquerda, botão Otimizar à direita, ocupando as duas linhas); e três colunas de cartões de configuração:

1. Configurações e Tecla de atalho.
2. Áreas de memória e Otimização automática.
3. Ícone da área de notificação e Processos excluídos.

A distância entre cartões é de 5px. No modo compacto, só o bloco de leitura permanece. O layout tem que aguentar textos longos de 30 idiomas e direção RTL.

## Elevation & Depth

Plano por padrão. A profundidade vem da camada tonal: fundo Mica, cartão levemente mais claro com contorno de 1px do tema, e o vermelho do botão. Não há sombras próprias.

## Shapes

Cartões com cantos de 8px e contorno de 1px. Controles seguem os cantos do tema Fluent. O mostrador é um círculo de 72px com traço de 3px; o ponteiro é uma barra reta de 4px por 29px com ponto central de 10px, ambos vermelhos.

## Components

### Buttons
- **Primary (Otimizar):** vermelho de sinal, texto branco em negrito 28px, 230px de largura, ocupa a altura das duas linhas do bloco de leitura.
- **Hover / Pressed:** vermelho mais escuro; foco por teclado segue o contorno Fluent.
- **Barra de título:** botões de ajuda, modo compacto, minimizar e fechar, transparentes e sem borda.

### Cards / Containers
- **Corner Style:** 8px.
- **Background:** preenchimento padrão de controle do tema Fluent.
- **Border:** 1px do tema.
- **Internal Padding:** 14px laterais, 10px em cima, 12px embaixo.

### Inputs / Fields
- **Style:** combobox, slider e checkbox Fluent; marcações e preenchimentos em vermelho de sinal.
- **Focus:** borda do combobox em vermelho.

### Mostrador de memória (Signature)
Anel de 72px com ponteiro vermelho cuja rotação é a porcentagem de memória física em uso (0–100% = uma volta). Vem acompanhado do valor em 44px e das barras de usado/livre.

## Do's and Don'ts

### Do:
- **Do** usar o vermelho de sinal apenas para ação, estado e seleção.
- **Do** manter o botão Otimizar sempre visível, inclusive no modo compacto.
- **Do** usar os controles Fluent do WPF-UI antes de criar controles novos.
- **Do** manter números de memória grandes e com unidade ao lado.

### Don't:
- **Don't** introduzir um segundo destaque colorido (verde/azul/laranja) para dados.
- **Don't** reintroduzir painéis preenchidos com cantos retos e cinza opaco como no design antigo.
- **Don't** usar glifos/emoji como ícones; usar ícones desenhados.
- **Don't** deixar o WPF-UI derivar o destaque da cor do sistema.
