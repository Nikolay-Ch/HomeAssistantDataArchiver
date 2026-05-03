# HomeAssistantDataArchiver (HomeAssistant-to-ClickHouse Archiver)

[RU] Высокопроизводительный .NET сервис для архивации данных Home Assistant в ClickHouse.  
[EN] High-performance .NET service for archiving Home Assistant data into ClickHouse.

---

## Общая идея проекта / Project Overview

### [RU]
Основная цель этого проекта — решение проблемы хранения огромных массивов исторических данных Home Assistant. Стандартная база (PostgreSQL/SQLite) при хранении данных за несколько лет неизбежно разрастается, что приводит к замедлению интерфейса и сложностям с бэкапами.

**Реальный кейс:** База Home Assistant, накопленная за 3 года, занимала **160 ГБ**. После миграции в ClickHouse через данный сервис, те же самые данные (с сохранением всей глубины истории) заняли всего **5 ГБ**. 

**Почему это работает?** ClickHouse — это колончатая СУБД, которая сжимает данные на порядок эффективнее классических баз и позволяет строить аналитические отчеты или графики в Grafana за миллисекунды даже на миллионах записей.

### [EN]
The primary goal of this project is to solve the long-term data storage issue in Home Assistant. Standard databases (PostgreSQL/SQLite) tend to bloat over years of usage, leading to UI lag and backup complications.

**Real-world case:** A Home Assistant database that grew to **160 GB** over 3 years was reduced to just **5 GB** in ClickHouse using this archiver, while keeping all historical records intact.

**Why it works:** ClickHouse is a column-oriented DBMS that provides exceptional data compression and allows building Grafana dashboards or analytical reports in milliseconds, even when processing millions of rows.

---

## Технические особенности / Technical Features

*   **Dual-Worker Architecture:**
    1.  **State Worker (Fast):** Воркер, запускаемый часто. Мониторит таблицу состояний (`states`) и оперативно переносит изменения в ClickHouse.
    2.  **Metadata Worker (Slow):** Воркер с низким приоритетом (запускается сильно реже). Синхронизирует структуру вашего дома: сущности (Entities), устройства (Devices), зоны (Areas) и этажи (Floors).
*   **Partitioning:** Данные состояний сенсоров в ClickHouse партиционируются по месяцам. Это позволяет мгновенно удалять старые данные целыми блоками, если они больше не нужны, без нагрузки на систему.
*   **Cross-platform:** Полная поддержка Windows и Linux. Благодаря .NET, сервис может быть запущен как системная служба (systemd), обычное консольное приложение или Docker-контейнер.
*   **Production Ready:** Сервис рассчитан на работу в фоне 24/7.

---

## Настройка / Configuration

Сервис достаточно гибок в настройке. Приоритет параметров: Переменные окружения > Файлы конфигурации.

### Переменные окружения (для Docker) / Environment Variables:

Для настройки через Docker используйте структуру `Секция__Параметр`. Пример описание части параметров ниже:

| Переменная (ENV) | Описание / Description |
| :--- | :--- |
| `HomeAssistant__Uri` | WebSocket адрес HA (e.g., `ws://192.168.1.10/api/websocket`) |
| `HomeAssistant__Token` | Long-lived Access Token из профиля HA |
| `Postgres__Host` | IP базы данных с данными Home Assistant |
| `Postgres__Database` | Имя базы (обычно `homeassistant`) |
| `ClickHouse__Host` | Хост вашего ClickHouse сервера |
| `ClickHouse__Port` | Порт ClickHouse (по умолчанию `8123`) |
| `Common__SensorDataMigrationBatchSize` | Размер пачки данных для одной миграции (default: `100000`) |

---

## Лицензия / License

[RU] Данное программное обеспечение распространяется на условиях лицензии **MIT**. Вы можете свободно использовать, копировать, модифицировать и распространять этот софт, при условии сохранения уведомления об авторстве. Продукт предоставляется «КАК ЕСТЬ», без каких-либо гарантий.

[EN] This software is released under the **MIT License**. You are free to use, copy, modify, and distribute this software as long as the original copyright notice is retained. The software is provided "AS IS", without warranty of any kind.
*See the [LICENSE](LICENSE.txt) file for the full text.*

---

## От автора / Author's Note

> [RU] Это мой первый проект, созданный при поддержке ИИ. НО!, это не "вайб-кодинг", где нейросеть пишет всё за человека. ИИ здесь использовался как сверхмощная энциклопедия и ассистент, позволяющий мгновенно находить специфические методы API и оптимальные способы трансформации данных, что значительно ускорило разработку.
>
> [EN] This is my first project developed with AI assistance. It’s not "vibe-coding" where the AI does everything; rather, the AI served as a high-powered encyclopedia and assistant, helping to quickly navigate API methods and data transformation patterns, significantly boosting development speed.
