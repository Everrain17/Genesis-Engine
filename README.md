# README.ru.md


# Genesis Engine

**Эмерджентная агентная симуляция развития цивилизаций**

Genesis Engine — программный комплекс агентного моделирования (ABM), в котором социальные, когнитивные и технологические структуры возникают из простых правил взаимодействия автономных агентов. В симуляции отсутствуют сценарии: язык, письменность, материалы, дипломатия и города не программируются явно, а появляются как результат потребностей агентов и свойств материалов.

Платформа предназначена для исследований в области вычислительной социологии, когнитивистики и эволюции культур, а также для генерации данных, пригодных для научных публикаций.

---

## Основные возможности

### Агенты
- Геном из 25+ параметров: Big Five (открытость, добросовестность, экстраверсия, доброжелательность, нейротизм), агрессия, храбрость, эмпатия, самосознание, духовность и др.
- Наследование генома с мутациями и рекомбинацией.
- Жизненный цикл: рождение, взросление, старение, смерть.
- Индивидуальная память: доверие к агентам, памятные места, паттерны действий.
- Роли (фермер, строитель и др.) определяются поведением, а не назначением.

### Когнитивные примитивы
- Субитизация и приближённое чувство числа (закон Вебера).
- Обнаружение причинности и повторяемости.
- Объектная перманентность и пространственная память.
- Категоризация и композиционность.
- Теория разума, агентность, иерархичность, модальность, ментальная временная шкала.

### Эмерджентный язык
Полная цепочка возникновения коммуникации:
1. Сигналы потребностей (еда, тревога, связь, торговля).
2. Лексикон — устойчивые ассоциации «сигнал → референт», уникальные для каждой цивилизации.
3. Грамматика — устойчивые последовательности сигналов.
4. Фонемы — повторяющиеся звуковые паттерны.
5. Графемы — визуальные символы для фонем (протописьменность).
6. Символические инварианты — контекстно-независимые правила (протоматематика).

### Материаловедение
- 6 фундаментальных параметров (энергия связи, плотность электронов, симметрия решётки и др.) порождают 16 наблюдаемых свойств.
- 50 базовых материалов и неограниченное пространство композитов.
- Нелинейные эффекты сплавов (упрочнение решётки, бронза, сталь, полупроводники).
- Классификация эмерджентных материалов по аналогам реального мира; фиксация материалов без реальных аналогов.

### Цивилизации и институты
- Автоматическая детекция цивилизаций по близости и доверию.
- Дипломатия: войны, союзы, торговые соглашения, усталость от войны.
- Строительство: фермы, дома, рынки, библиотеки, храмы и др.
- Институты знаний, передача знаний (обучение, чтение текстов, наследование).
- Культура: артефакты, тексты, священные места, траур.

### Научный инструментарий
- Полный детерминизм при фиксированном сиде.
- Экспорт данных в CSV (демография, эры, знания, сигналы, производительность).
- Текстовый журнал событий с уровнями логирования.
- Встроенный профайлер производительности.
- Автоматическая классификация эр и научных эпох.

---

## Требования

- .NET SDK 8.0
- Для графического режима — Raylib (устанавливается как NuGet-зависимость автоматически)

## Сборка

```bash
dotnet restore
dotnet build --configuration Release
```

## Запуск

Графический режим:

```bash
dotnet run
```

Headless-режим (эксперименты):

```bash
dotnet run -- --headless --seed 42 --agents 200 --ticks 10000
```

## Параметры командной строки

| Параметр            | Описание                              | Значение по умолчанию |
|---------------------|---------------------------------------|------------------------|
| `--headless`        | Запуск без графического окна          | выключено              |
| `--seed <int>`      | Фиксированный сид (детерминизм)       | случайный              |
| `--agents <int>`    | Начальная численность агентов         | 150                    |
| `--ticks <int>`     | Ограничение числа тиков               | бесконечно             |
| `--quiet`           | Отключить вывод в консоль             | выключено              |
| `--loglevel <уров.>`| Уровень журнала (info/warn/war/death/error) | info             |

Управление в графическом режиме: `SPACE` — пауза, `1/2/3` — скорость, `R` — аналитика, колесо мыши — масштаб.

## Выходные данные

| Файл                                  | Содержимое                                                              |
|---------------------------------------|-------------------------------------------------------------------------|
| `data/emergence_data_<RunId>.csv`     | Население, эра, фермы, поселения, средняя твёрдость инструментов        |
| `data/headless_status_<RunId>.csv`    | Население, цивилизации, знания, тексты, сигналы, время тика             |
| `logs/log_<дата>.txt`                 | Полный журнал событий (открытия, войны, прорывы, потери знаний)         |

---

## Наблюдаемые феномены

В ходе прогонов с различными сидами зафиксированы следующие эмерджентные явления:

- **Цивилизационные циклы** — рост, коллапс («тёмные века») и возрождение популяции с консолидацией цивилизаций.
- **Ловушка знаний** — накопленные знания сохраняются в текстах, но не используются при деградации институтов.
- **Эмерджентный язык** — до 700+ устойчивых слов и 40+ грамматических правил на цивилизацию; каждая цивилизация формирует собственный диалект.
- **Протописьменность** — создание графем для устойчивых фонем.
- **Материалы без реальных аналогов** — композиты, не сопоставимые с базой реальных материалов.
- **Урбанизация** — до 200+ эмерджентных поселений без какого-либо планирования сверху.

---

## Структура проекта

```
Project/
├── Core/               # Сид-генератор, вектора, перечисления, профайлер
├── Entities/           # Агент, геном, память, существа
├── Systems/
│   ├── Analytics/      # Экспорт CSV-данных
│   ├── Behaviour/      # Манипуляции объектами, наблюдение
│   ├── Biology/        # Метаболизм, голод, энергия
│   ├── Emergence/      # Классификация паттернов и эр
│   ├── Observers/      # Шина событий и наблюдатели
│   ├── Physics/        # Фундаментальные параметры, материалы, анализатор
│   ├── ...             # Язык, грамматика, фонемы, графемы, символика,
│                       # дипломатия, торговля, строительство, логика
├── UI/                 # Журнал, графическое окно (Raylib)
├── World/              # Тайл, генератор мира
└── Simulation.cs       # Главный цикл симуляции
```

---

## Дорожная карта

- Конфигурируемые сценарии (YAML) и domain-agnostic режим платформы.
- Модуль стресс-тестирования распределённых систем.
- Модуль моделирования рисков человеческого фактора.
- Визуализация данных и веб-интерфейс экспериментов.
- Публикации в рецензируемых изданиях (JASSS, Artificial Life, Adaptive Behavior).

---

# README.md (English Version)


# Genesis Engine

**Emergent agent-based simulation of civilizational development**

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-12-purple)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Raylib](https://img.shields.io/badge/Raylib-5.0-orange)](https://www.raylib.com/)

---

## About

Genesis Engine is an Agent-Based Modeling (ABM) platform that simulates the emergence of civilization from first principles. Autonomous agents with individual genomes interact through the **sense → decide → act** cycle, and all macro-phenomena — language, writing, technology, diplomacy, urbanization — emerge without being explicitly programmed.

The platform is designed for research in computational sociology, cognitive science, and cultural evolution, and produces data suitable for scientific publication.

### Core Hypothesis

> **The Knowledge Trap**: Civilizations can accumulate and preserve knowledge, yet lose the capacity to utilize it when institutional infrastructure degrades below a critical threshold.

---

## Features

### Agents
- Genome with 25+ parameters: Big Five personality traits (Openness, Conscientiousness, Extraversion, Agreeableness, Neuroticism), aggression, courage, empathy, self-awareness, spirituality, and more
- Genetic inheritance with mutation and recombination
- Full life cycle: birth, maturation, aging, death
- Individual memory: trust toward other agents, place memory, action patterns

### Cognitive Primitives
- Subitization and approximate number sense (Weber's law)
- Causality and repetition detection
- Object permanence and spatial memory
- Categorization and compositionality
- Theory of mind, agency detection, hierarchy recognition, modality reasoning, mental timeline

### Emergent Language
A complete pipeline for communication emergence:
1. **Signals** — needs-based emissions (food, alarm, bonding, trade)
2. **Lexicon** — stable signal-to-referent associations, unique per civilization
3. **Grammar** — stable signal sequences with semantic weight
4. **Phonemes** — recurring sound patterns
5. **Graphemes** — visual symbols for phonemes (proto-writing)
6. **Symbolic invariants** — context-independent rules (proto-mathematics)

### Material Science
- 6 fundamental parameters (bond energy, electron density, lattice symmetry, atomic mass, thermal vibration, quantum coherence) derive 16 observable properties
- 50 base materials and unlimited composite space
- Nonlinear alloy effects (lattice distortion, bronze, steel, semiconductor, superconductor)
- Classification of emergent materials against real-world analogs; detection of materials with no real-world equivalent

### Civilizations and Institutions
- Automatic civilization detection based on proximity and trust
- Diplomacy: wars, alliances, trade agreements, non-aggression pacts, war weariness
- Construction: farms, houses, markets, libraries, temples, and more
- Knowledge institutions, knowledge transmission (teaching, reading texts, inheritance)
- Culture: artifacts, written texts, sacred sites, mourning rituals

### Scientific Instrumentation
- Full determinism via fixed seeds
- CSV data export (demography, eras, knowledge, signals, performance)
- Event logging with configurable levels
- Background logging thread — disk I/O never blocks the simulation
- Built-in performance profiler
- Automatic era and scientific epoch classification

---

## Requirements

- .NET SDK 8.0
- For graphical mode — Raylib (installed automatically as a NuGet dependency)

## Build

```bash
dotnet restore
dotnet build --configuration Release
```

## Run

Graphical mode:

```bash
dotnet run
```

Headless mode (experiments):

```bash
dotnet run -- --headless --seed 42 --agents 200 --ticks 10000
```

## Command-Line Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `--headless` | Run without graphical window | off |
| `--seed <int>` | Fixed seed (determinism) | random |
| `--agents <int>` | Initial agent count | 150 |
| `--ticks <int>` | Tick limit | infinite |
| `--quiet` | Suppress console output | off |
| `--loglevel <level>` | Log level (info/warn/war/death/error) | info |

Graphical mode controls: `SPACE` — pause, `1/2/3` — speed, `R` — analytics, mouse wheel — zoom.

## Output Data

| File | Content |
|------|---------|
| `data/emergence_data_<RunId>.csv` | Population, era, farms, settlements, average tool hardness |
| `data/headless_status_<RunId>.csv` | Population, civilizations, knowledge, texts, signals, tick time |
| `logs/log_<date>.txt` | Full event log (discoveries, wars, breakthroughs, knowledge loss) |

---

## Observed Phenomena

The following emergent phenomena have been observed across multiple runs with different seeds:

- **Civilizational cycles** — growth, collapse ("dark ages"), and renaissance with civilization consolidation
- **Knowledge Trap** — accumulated knowledge persists in texts but becomes inaccessible when institutions degrade
- **Emergent language** — up to 700+ stable words and 40+ grammar rules per civilization; each civilization develops its own dialect
- **Proto-writing** — creation of graphemes for stable phonemes
- **Materials without real-world analogs** — composite materials that do not correspond to any entry in the real-world material database
- **Urbanization** — up to 200+ emergent settlements without any top-down planning

---

## Project Structure

```
Project/
├── Core/               # RNG, vectors, enumerations, profiler
├── Entities/           # Agent, genome, memory, creatures
├── Systems/
│   ├── Analytics/      # CSV data export
│   ├── Behaviour/      # Object manipulation, observation
│   ├── Biology/        # Metabolism, hunger, energy
│   ├── Emergence/      # Pattern and era classification
│   ├── Observers/      # Event bus and observers
│   ├── Physics/        # Fundamental parameters, materials, analyzer
│   ├── ...             # Language, grammar, phonemes, graphemes, symbols,
│                       # diplomacy, trade, construction, logic
├── UI/                 # Logger, graphical window (Raylib)
├── World/              # Tile, world generator
└── Simulation.cs       # Main simulation loop
```

---

## Roadmap

- Configurable scenarios (YAML) and domain-agnostic platform mode
- Distributed systems stress-testing module
- Human factor risk modeling module
- Data visualization and web experiment interface
- Publications in peer-reviewed venues (JASSS, Artificial Life, Adaptive Behavior)

---

