
# README.ru.md

# Genesis Engine

**Эмерджентная агентная симуляция развития цивилизаций от каменного века до паровой эпохи**

Genesis Engine — программный комплекс агентного моделирования (ABM), в котором социальные, когнитивные и технологические структуры возникают из простых правил взаимодействия автономных агентов. В симуляции отсутствуют сценарии: язык, письменность, материалы, дипломатия, города, эпидемии и погода не программируются явно, а появляются как результат потребностей агентов, свойств материалов и эмерджентных взаимодействий.

Платформа предназначена для исследований в области вычислительной социологии, когнитивистики, эволюции культур и эпидемиологии, а также для генерации данных, пригодных для научных публикаций.

---

## Основные возможности

### Агенты
- **Геном из 25+ параметров**: Big Five (открытость, добросовестность, экстраверсия, доброжелательность, нейротизм), агрессия, храбрость, эмпатия, самосознание, духовность, плодовитость, иммунитет и др.
- **Наследование генома** с мутациями и рекомбинацией
- **Генетические устойчивости** к патогенам, формирующиеся через переживание болезней
- **Жизненный цикл**: рождение, взросление, старение, смерть
- **Индивидуальная память**: доверие к агентам, памятные места, паттерны действий, история действий
- **Эмерджентные роли** (фермер, строитель, торговец, солдат, учёный, ремесленник), определяемые поведением, а не назначением
- **Когнитивные состояния**: страх, любопытство, одиночество, отчаяние

### Когнитивные примитивы
- **Субитизация** и приближённое чувство числа (закон Вебера)
- **Обнаружение причинности** и повторяемости
- **Объектная перманентность** и пространственная память
- **Категоризация** и композиционность
- **Теория разума**, агентность, иерархичность, модальность, ментальная временная шкала
- **Экологическая дедукция** — эмерджентное понимание влияния среды на выживание

### Эмерджентный язык
Полная цепочка возникновения коммуникации:
1. **Сигналы потребностей** (еда, тревога, связь, торговля, помощь, скорбь, праздник)
2. **Лексикон** — устойчивые ассоциации «сигнал → референт», уникальные для каждой цивилизации
3. **Грамматика** — устойчивые последовательности сигналов с валентностью
4. **Фонемы** — повторяющиеся звуковые паттерны
5. **Графемы** — визуальные символы для фонем (протописьменность)
6. **Символические инварианты** — контекстно-независимые правила (протоматематика)

### Материаловедение
- **6 фундаментальных параметров** (энергия связи, плотность электронов, симметрия решётки, атомная масса, тепловые колебания, квантовая когерентность) порождают **16 наблюдаемых свойств**
- **50 базовых материалов** и неограниченное пространство композитов
- **Нелинейные эффекты сплавов** (упрочнение решётки, бронза, сталь, полупроводники, сверхпроводники)
- **Классификация эмерджентных материалов** по аналогам реального мира; фиксация материалов без реальных аналогов
- **Процедурная генерация ресурсов** на основе свойств материалов и типа биома

### Цивилизации и институты
- **Автоматическая детекция цивилизаций** по близости и доверию
- **Дипломатия**: войны, союзы, торговые соглашения, пакты о ненападении, вассалитет, усталость от войны
- **Захват территорий** — эмерджентное расширение влияния цивилизаций
- **Строительство**: фермы, дома, рынки, библиотеки, храмы, шахты, мосты, склады, хосписы
- **Институты знаний**, передача знаний (обучение, чтение текстов, наследование)
- **Культура**: артефакты, тексты, священные места, траур, праздники
- **Социальное неравенство** и риск восстаний (коэффициент Джини)

### Эпидемиология
- **Эмерджентные патогены**, привязанные к биомам (болотная лихорадка, пустынная оспа, тундрозный холод и др.)
- **Мутации вирусов** — дрейф свойств (вирулентность, заразность)
- **Приобретённый иммунитет** и генетическая ассимиляция устойчивости
- **Стадный иммунитет** и отслеживание вымерших штаммов
- **Эмерджентное избегание болезней** — агенты учатся избегать заражённых

### Погода и сезоны
- **Глобальные погодные параметры**: температура, влажность, ветер, осадки, давление
- **4 времени года** (весна, лето, осень, зима) влияют на урожайность, метаболизм, строительство, торговлю
- **Локальные погодные эффекты**: эрозия, конденсация, высушивание, заморозка
- **Влияние на агентов**: урон от холода/жары, стресс, дискомфорт, шанс простуды

### Научный инструментарий
- **Полный детерминизм** при фиксированном сиде
- **Экспорт данных** в CSV (демография, эры, знания, сигналы, производительность, эпидемии, погода, дипломатия, культура, материалы, технологии)
- **Текстовый журнал событий** с уровнями логирования (инфо, предупреждение, война, смерть, ошибка)
- **Фоновый поток логирования** — ввод-вывод на диск никогда не блокирует симуляцию
- **Встроенный профайлер производительности**
- **Автоматическая классификация эр** и научных эпох
- **Экспорт в Excel** — автоматическое создание отчёта из всех CSV-файлов

---

## Требования

- **.NET SDK 8.0**
- Для графического режима — **Raylib** (устанавливается как NuGet-зависимость автоматически)

## Сборка

```bash
dotnet restore
dotnet build --configuration Release
```

## Запуск

**Графический режим:**

```bash
dotnet run
```

**Headless-режим** (эксперименты):

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

**Управление в графическом режиме:** `SPACE` — пауза, `1/2/3` — скорость, `R` — аналитика, `V` — возврат из детального просмотра, колесо мыши — масштаб, левая кнопка мыши — перемещение камеры.

## Выходные данные

| Файл                                  | Содержимое                                                              |
|---------------------------------------|-------------------------------------------------------------------------|
| `data/emergence_data_<RunId>.csv`     | Население, эра, фермы, поселения, средняя твёрдость инструментов, заражённые, стадный иммунитет, сезон |
| `data/headless_status_<RunId>.csv`    | Население, цивилизации, знания, тексты, сигналы, время тика             |
| `data/extended_data_<RunId>.csv`      | Расширенные метрики: грамотность, институты, лексикон, грамматика, фонемы, графемы, инварианты, здания, неравенство, погода |
| `data/demography_data_<RunId>.csv`    | Рождаемость, смертность (голод, хищники, бой, естественная, чума, холод), средний возраст, поколения, роли |
| `data/civ_snapshots_<RunId>.csv`      | Снимки цивилизаций: население, эра, знания, развитие, структуры, очки   |
| `data/events_<RunId>.csv`             | События: рождения, смерти, торговля, бой, рейды, открытия, постройки, артефакты, материалы |
| `data/cognitive_data_<RunId>.csv`     | Когнитивные метрики: категории, пространство, теория разума, временные последовательности |
| `data/signals_data_<RunId>.csv`       | Активные сигналы: тревога, еда, приход, опасность, торговля, помощь, связь, скорбь, праздник |
| `data/materials_data_<RunId>.csv`     | Материалы: базовые, композиты, прорывы, аналоги, вычислительная мощность |
| `data/technology_data_<RunId>.csv`    | Технологические оси и их средняя мощность                               |
| `data/diplomacy_data_<RunId>.csv`     | Дипломатические отношения: пары, войны, давление войны, стабильность мира |
| `data/culture_data_<RunId>.csv`       | Культура: артефакты, священные артефакты, тексты, священные тексты, символы, святость |
| `data/performance_data_<RunId>.csv`   | Производительность: время тика, агенты, существа, цивилизации, обработанные события |
| `data/pathogen_data_<RunId>.csv`      | Патогены: штаммы, заражённые, умершие, пиковая активность, вирулентность, заразность, вымирание |
| `logs/log_<дата>.txt`                 | Полный журнал событий (открытия, войны, прорывы, потери знаний, мутации вирусов, погода) |
| `data/report.xlsx`                    | Автоматически сгенерированный Excel-отчёт со всеми данными              |

---

## Наблюдаемые феномены

В ходе прогонов с различными сидами зафиксированы следующие эмерджентные явления:

- **Цивилизационные циклы** — рост, коллапс («тёмные века») и возрождение популяции с консолидацией цивилизаций
- **Ловушка знаний** — накопленные знания сохраняются в текстах, но не используются при деградации институтов
- **Эмерджентный язык** — до 700+ устойчивых слов и 40+ грамматических правил на цивилизацию; каждая цивилизация формирует собственный диалект
- **Протописьменность** — создание графем для устойчивых фонем
- **Материалы без реальных аналогов** — композиты, не сопоставимые с базой реальных материалов
- **Урбанизация** — до 200+ эмерджентных поселений без какого-либо планирования сверху
- **Эпидемии** — вспышки болезней с мутациями вирусов, формированием стадного иммунитета и генетической адаптацией
- **Восстания** — социальные перевороты при высоком неравенстве (коэффициент Джини > 0.65)
- **Погодные аномалии** — экстремальная жара, холод, ветер, осадки, влияющие на выживание и развитие
- **Сезонные миграции** — агенты адаптируются к временам года, строят укрытия, запасают еду
- **Логические устройства** — агенты эмерджентно собирают логические вентили и экспериментируют с ними
- **Символическая математика** — абстрактные правила, независимые от контекста

---

## Структура проекта

```
Project/
── Core/               # Сид-генератор, вектора, перечисления, профайлер
├── Entities/           # Агент, геном, память, существа
├── Systems/
│   ├── Analytics/      # Экспорт CSV-данных, Excel-отчёты
│   ├── Behaviour/      # Манипуляции объектами, наблюдение
│   ├── Biology/        # Метаболизм, голод, энергия
│   ├── Emergence/      # Классификация паттернов и эр
│   ├── Observers/      # Шина событий и наблюдатели
│   ├── Physics/        # Фундаментальные параметры, материалы, анализатор
│   ├── ...             # Язык, грамматика, фонемы, графемы, символика,
│   │                    дипломатия, торговля, строительство, логика,
│   │                    эпидемии, погода, сезоны, неравенство, восстания
├── UI/                 # Журнал, графическое окно (Raylib)
── World/              # Тайл, генератор мира
└── Simulation.cs       # Главный цикл симуляции
```

---

## Научная ценность

Проект готов к использованию в **Agent-Based Modeling** для:
- Исследования эмерджентности социальных структур
- Моделирования распространения знаний и инноваций
- Изучения динамики эпидемий и эволюции иммунитета
- Анализа влияния климата и сезонов на развитие цивилизаций
- Тестирования гипотез о возникновении языка и письменности
- Моделирования социального неравенства и конфликтов

**Собираются данные для статей**

---

## Дорожная карта

- Конфигурируемые сценарии (YAML) и domain-agnostic режим платформы
- Модуль стресс-тестирования распределённых систем
- Модуль моделирования рисков человеческого фактора
- Визуализация данных и веб-интерфейс экспериментов
- Торговые пути и экономические сети
- Религиозные движения и идеологии

---

# README.md (English Version)

# Genesis Engine

**Emergent agent-based simulation of civilizational development from Stone Age to Steam Age**

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-12-purple)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Raylib](https://img.shields.io/badge/Raylib-5.0-orange)](https://www.raylib.com/)

---

## About

Genesis Engine is an Agent-Based Modeling (ABM) platform that simulates the emergence of civilization from first principles. Autonomous agents with individual genomes interact through the **sense → decide → act** cycle, and all macro-phenomena — language, writing, technology, diplomacy, urbanization, epidemics, weather — emerge without being explicitly programmed.

The platform is designed for research in computational sociology, cognitive science, cultural evolution, and epidemiology, and produces data suitable for scientific publication.

### Core Hypothesis

> **The Knowledge Trap**: Civilizations can accumulate and preserve knowledge, yet lose the capacity to utilize it when institutional infrastructure degrades below a critical threshold.

---

## Features

### Agents
- **Genome with 25+ parameters**: Big Five personality traits (Openness, Conscientiousness, Extraversion, Agreeableness, Neuroticism), aggression, courage, empathy, self-awareness, spirituality, fertility, immune strength, and more
- **Genetic inheritance** with mutation and recombination
- **Genetic resistances** to pathogens, formed through surviving diseases
- **Full life cycle**: birth, maturation, aging, death
- **Individual memory**: trust toward other agents, place memory, action patterns, action history
- **Emergent roles** (Farmer, Builder, Trader, Soldier, Scholar, Artisan) determined by behavior, not assignment
- **Cognitive states**: fear, curiosity, loneliness, despair

### Cognitive Primitives
- **Subitization** and approximate number sense (Weber's law)
- **Causality and repetition detection**
- **Object permanence** and spatial memory
- **Categorization** and compositionality
- **Theory of mind**, agency detection, hierarchy recognition, modality reasoning, mental timeline
- **Environmental deduction** — emergent understanding of environmental impact on survival

### Emergent Language
A complete pipeline for communication emergence:
1. **Signals** — needs-based emissions (food, alarm, bonding, trade, help, mourn, celebrate)
2. **Lexicon** — stable signal-to-referent associations, unique per civilization
3. **Grammar** — stable signal sequences with valence
4. **Phonemes** — recurring sound patterns
5. **Graphemes** — visual symbols for phonemes (proto-writing)
6. **Symbolic invariants** — context-independent rules (proto-mathematics)

### Material Science
- **6 fundamental parameters** (bond energy, electron density, lattice symmetry, atomic mass, thermal vibration, quantum coherence) derive **16 observable properties**
- **50 base materials** and unlimited composite space
- **Nonlinear alloy effects** (lattice distortion, bronze, steel, semiconductor, superconductor)
- **Classification of emergent materials** against real-world analogs; detection of materials with no real-world equivalent
- **Procedural resource generation** based on material properties and biome type

### Civilizations and Institutions
- **Automatic civilization detection** based on proximity and trust
- **Diplomacy**: wars, alliances, trade agreements, non-aggression pacts, vassalage, war weariness
- **Territory capture** — emergent expansion of civilizational influence
- **Construction**: farms, houses, markets, libraries, temples, mines, bridges, warehouses, hospices
- **Knowledge institutions**, knowledge transmission (teaching, reading texts, inheritance)
- **Culture**: artifacts, written texts, sacred sites, mourning, celebrations
- **Social inequality** and revolt risk (Gini coefficient)

### Epidemiology
- **Emergent pathogens** tied to biomes (marsh-fever, desert-pox, tundra-chill, etc.)
- **Virus mutations** — drift in properties (virulence, contagiousness)
- **Acquired immunity** and genetic assimilation of resistance
- **Herd immunity** and tracking of extinct strains
- **Emergent disease avoidance** — agents learn to avoid infected individuals

### Weather and Seasons
- **Global weather parameters**: temperature, humidity, wind, precipitation, pressure
- **4 seasons** (Spring, Summer, Autumn, Winter) affecting fertility, metabolism, construction, trade
- **Local weather effects**: erosion, condensation, drying, freezing
- **Impact on agents**: cold/heat damage, stress, discomfort, chance of illness

### Scientific Instrumentation
- **Full determinism** via fixed seeds
- **CSV data export** (demography, eras, knowledge, signals, performance, epidemics, weather, diplomacy, culture, materials, technology)
- **Event logging** with configurable levels (info, warn, war, death, error)
- **Background logging thread** — disk I/O never blocks the simulation
- **Built-in performance profiler**
- **Automatic era and scientific epoch classification**
- **Excel export** — automatic report generation from all CSV files

---

## Requirements

- **.NET SDK 8.0**
- For graphical mode — **Raylib** (installed automatically as a NuGet dependency)

## Build

```bash
dotnet restore
dotnet build --configuration Release
```

## Run

**Graphical mode:**

```bash
dotnet run
```

**Headless mode** (experiments):

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

**Graphical mode controls:** `SPACE` — pause, `1/2/3` — speed, `R` — analytics, `V` — back from detail view, mouse wheel — zoom, left mouse button — camera pan.

## Output Data

| File | Content |
|------|---------|
| `data/emergence_data_<RunId>.csv` | Population, era, farms, settlements, average tool hardness, infected, herd immunity, season |
| `data/headless_status_<RunId>.csv` | Population, civilizations, knowledge, texts, signals, tick time |
| `data/extended_data_<RunId>.csv` | Extended metrics: literacy, institutions, lexicon, grammar, phonemes, graphemes, invariants, buildings, inequality, weather |
| `data/demography_data_<RunId>.csv` | Births, deaths (hunger, predator, combat, natural, plague, cold), avg age, generations, roles |
| `data/civ_snapshots_<RunId>.csv` | Civilization snapshots: population, era, knowledge, development, structures, score |
| `data/events_<RunId>.csv` | Events: births, deaths, trade, combat, raids, discoveries, buildings, artifacts, materials |
| `data/cognitive_data_<RunId>.csv` | Cognitive metrics: categories, spatial, theory of mind, temporal sequences |
| `data/signals_data_<RunId>.csv` | Active signals: alarm, food, come, danger, trade, help, bond, mourn, celebrate |
| `data/materials_data_<RunId>.csv` | Materials: base, composites, breakthroughs, analogs, computation capacity |
| `data/technology_data_<RunId>.csv` | Technology axes and their average capacity |
| `data/diplomacy_data_<RunId>.csv` | Diplomatic relations: pairs, wars, war pressure, peace stability |
| `data/culture_data_<RunId>.csv` | Culture: artifacts, sacred artifacts, texts, sacred texts, symbols, sanctity |
| `data/performance_data_<RunId>.csv` | Performance: tick time, agents, creatures, civilizations, processed events |
| `data/pathogen_data_<RunId>.csv` | Pathogens: strains, infected, died, peak active, virulence, contagiousness, extinction |
| `logs/log_<date>.txt` | Full event log (discoveries, wars, breakthroughs, knowledge loss, virus mutations, weather) |
| `data/report.xlsx` | Auto-generated Excel report with all data |

---

## Observed Phenomena

The following emergent phenomena have been observed across multiple runs with different seeds:

- **Civilizational cycles** — growth, collapse ("dark ages"), and renaissance with civilization consolidation
- **Knowledge Trap** — accumulated knowledge persists in texts but becomes inaccessible when institutions degrade
- **Emergent language** — up to 700+ stable words and 40+ grammar rules per civilization; each civilization develops its own dialect
- **Proto-writing** — creation of graphemes for stable phonemes
- **Materials without real-world analogs** — composite materials that do not correspond to any entry in the real-world material database
- **Urbanization** — up to 200+ emergent settlements without any top-down planning
- **Epidemics** — disease outbreaks with virus mutations, herd immunity formation, and genetic adaptation
- **Revolts** — social upheavals at high inequality (Gini coefficient > 0.65)
- **Weather anomalies** — extreme heat, cold, wind, precipitation affecting survival and development
- **Seasonal migrations** — agents adapt to seasons, build shelters, stockpile food
- **Logic devices** — agents emergently assemble logic gates and experiment with them
- **Symbolic mathematics** — abstract rules independent of context

---

## Project Structure

```
Project/
├── Core/               # RNG, vectors, enumerations, profiler
├── Entities/           # Agent, genome, memory, creatures
├── Systems/
│   ├── Analytics/      # CSV data export, Excel reports
│   ├── Behaviour/      # Object manipulation, observation
│   ├── Biology/        # Metabolism, hunger, energy
│   ├── Emergence/      # Pattern and era classification
│   ├── Observers/      # Event bus and observers
│   ├── Physics/        # Fundamental parameters, materials, analyzer
│   ├── ...             # Language, grammar, phonemes, graphemes, symbols,
│   │                    diplomacy, trade, construction, logic,
│   │                    epidemics, weather, seasons, inequality, revolts
├── UI/                 # Logger, graphical window (Raylib)
├── World/              # Tile, world generator
└── Simulation.cs       # Main simulation loop
```

---

## Scientific Value

The project is ready for use in **Agent-Based Modeling** for:
- Researching emergence of social structures
- Modeling knowledge and innovation diffusion
- Studying epidemic dynamics and immunity evolution
- Analyzing climate and seasonal impact on civilizational development
- Testing hypotheses about language and writing emergence
- Modeling social inequality and conflicts

**Data is being collected for publications**

---

## Roadmap

- Configurable scenarios (YAML) and domain-agnostic platform mode
- Distributed systems stress-testing module
- Human factor risk modeling module
- Data visualization and web experiment interface
- Trade routes and economic networks
- Religious movements and ideologies
