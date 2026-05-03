using HomeAssistantDataArchiver.HomeAssistant;

namespace HomeAssistantDataArchiver.DB;

public enum TableNames
{
    sensors_archive = 1,
    entities_archive = 2
}

internal class DatabaseQuerires
{
    public static string GetLastStateTimestampQuery() => @"SELECT max(toUnixTimestamp64Milli(time) / 1000.0) FROM ha_archive.states;";

    public static string GetPostgreReadStateQuery() => @"
        SELECT s.last_updated_ts, m.entity_id, s.state, a.shared_attrs 
        FROM states s 
        LEFT JOIN states_meta m ON s.metadata_id = m.metadata_id 
        LEFT JOIN state_attributes a ON s.attributes_id = a.attributes_id 
        WHERE s.last_updated_ts > @lastTs
        ORDER BY s.last_updated_ts ASC";

    public static string GetClickHouseTableName(HomeAssistantTableType table) => table switch
    {
        HomeAssistantTableType.Entity => "ha_archive.entities",
        HomeAssistantTableType.Device => "ha_archive.devices",
        HomeAssistantTableType.Area => "ha_archive.areas",
        HomeAssistantTableType.Floor => "ha_archive.floors",
        HomeAssistantTableType.State => "ha_archive.states",

        _ => throw new ArgumentOutOfRangeException(nameof(table), table, null)
    };

    public static string GetClickHouseCreateScript(HomeAssistantTableType table) => table switch
    {
        HomeAssistantTableType.Entity => @"
            CREATE TABLE IF NOT EXISTS ha_archive.entities (
                entity_id LowCardinality(String),
                id String,
                unique_id String,
                device_id LowCardinality(String),
                area_id LowCardinality(String),
                config_entry_id Nullable(String),
                config_subentry_id Nullable(String),
                name LowCardinality(String),
                original_name LowCardinality(String),
                icon LowCardinality(String),
                platform LowCardinality(String),
                entity_category LowCardinality(String),
                translation_key Nullable(String),
                disabled_by Nullable(String),
                hidden_by Nullable(String),
                has_entity_name Bool,
                labels Array(String),
                categories Map(String, String),
                options JSON,
                created_at DateTime64(3, 'UTC'),
                modified_at DateTime64(3, 'UTC'),
                updated_at DateTime DEFAULT now()
            ) ENGINE = ReplacingMergeTree(updated_at)
            ORDER BY (entity_id);",

        HomeAssistantTableType.Device => @"
            CREATE TABLE IF NOT EXISTS ha_archive.devices (
                id String,
                name LowCardinality(String),
                name_by_user LowCardinality(String),
                manufacturer LowCardinality(String),
                model LowCardinality(String),
                model_id LowCardinality(String),
                sw_version LowCardinality(String),
                hw_version LowCardinality(String),
                serial_number Nullable(String),
                area_id LowCardinality(String),
                via_device_id Nullable(String),
                identifiers Array(Array(String)),
                connections Array(Array(String)),
                labels Array(LowCardinality(String)),
                created_at DateTime64(3, 'UTC'),
                modified_at DateTime64(3, 'UTC'),
                updated_at DateTime DEFAULT now()
            ) ENGINE = ReplacingMergeTree(updated_at)
            ORDER BY (id)",

        HomeAssistantTableType.Area => @"
            CREATE TABLE IF NOT EXISTS ha_archive.areas (
                area_id LowCardinality(String),
                name LowCardinality(String),
                icon LowCardinality(String),
                picture Nullable(String),
                floor_id LowCardinality(String),
                temperature_entity_id Nullable(String),
                humidity_entity_id Nullable(String),
                aliases Array(String),
                labels Array(String),
                created_at DateTime64(3, 'UTC'),
                modified_at DateTime64(3, 'UTC'),
                updated_at DateTime DEFAULT now()
            ) ENGINE = ReplacingMergeTree(updated_at)
            ORDER BY (area_id)",

        HomeAssistantTableType.Floor => @"
            CREATE TABLE IF NOT EXISTS ha_archive.floors (
                floor_id LowCardinality(String),
                name LowCardinality(String),
                level Int8 DEFAULT 0,
                icon LowCardinality(String),
                aliases Array(String),
                created_at DateTime64(3, 'UTC'),
                modified_at DateTime64(3, 'UTC'),
                updated_at DateTime DEFAULT now()
            ) ENGINE = ReplacingMergeTree(updated_at)
            ORDER BY (floor_id)",

        HomeAssistantTableType.State => @"
            CREATE TABLE IF NOT EXISTS ha_archive.states (
                time DateTime64(3, 'UTC'),
                entity_id LowCardinality(String),
                value_type Enum8('i' = 1, 'f' = 2, 'b' = 3, 's' = 4),
                val_int Int64 DEFAULT NULL,
                val_float Float64 DEFAULT NULL,
                val_bool Bool DEFAULT NULL,
                val_string String DEFAULT NULL,
                val_attributes JSON DEFAULT NULL
            ) ENGINE = ReplacingMergeTree(time)
            PARTITION BY toYYYYMM(time)
            ORDER BY (entity_id, time);",

        _ => throw new ArgumentOutOfRangeException(nameof(table))
    };
}
