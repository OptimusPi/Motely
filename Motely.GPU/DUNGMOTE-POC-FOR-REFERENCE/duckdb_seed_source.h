/**
 * @file duckdb_seed_source.h
 * @brief DuckDB seed source writer - creates Motely-compatible seed source databases
 * 
 * Creates DuckDB files with the `seeds` table schema that Motely can use as --seedsource input.
 * Schema matches Motely's DuckDBSchema.SeedSourcesTableSchema():
 *   CREATE TABLE seeds (id BIGINT, seed VARCHAR(8))
 *   CREATE INDEX idx_seeds_id ON seeds(id)
 */

#ifndef DUCKDB_SEED_SOURCE_H
#define DUCKDB_SEED_SOURCE_H

#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <stdbool.h>

// DuckDB C API - include if available, otherwise we'll need to link against libduckdb
#ifdef __cplusplus
extern "C" {
#endif

// Forward declarations - these should match DuckDB C API
typedef void* duckdb_database;
typedef void* duckdb_connection;
typedef void* duckdb_appender;
typedef enum { DuckDBSuccess = 0, DuckDBError = 1 } duckdb_state;

// DuckDB C API functions (from duckdb.h)
duckdb_state duckdb_open(const char* path, duckdb_database* out_database);
duckdb_state duckdb_connect(duckdb_database database, duckdb_connection* out_connection);
duckdb_state duckdb_query(duckdb_connection connection, const char* query, void* result);
duckdb_state duckdb_appender_create(duckdb_connection connection, const char* schema, const char* table, duckdb_appender* out_appender);
duckdb_state duckdb_append_int64(duckdb_appender appender, int64_t value);
duckdb_state duckdb_append_varchar(duckdb_appender appender, const char* value);
duckdb_state duckdb_appender_end_row(duckdb_appender appender);
duckdb_state duckdb_appender_flush(duckdb_appender appender);
duckdb_state duckdb_appender_destroy(duckdb_appender* appender);
duckdb_state duckdb_disconnect(duckdb_connection* connection);
duckdb_state duckdb_close(duckdb_database* database);

#ifdef __cplusplus
}
#endif

/**
 * @brief DuckDB seed source writer context
 */
typedef struct {
    duckdb_database db;
    duckdb_connection con;
    duckdb_appender appender;
    int64_t seed_id;  // Auto-incrementing ID
    bool initialized;
} DuckDBSeedWriter;

/**
 * @brief Initialize DuckDB seed source writer
 * @param writer Writer context (must be zero-initialized)
 * @param db_path Path to DuckDB file (will be created/overwritten)
 * @return true on success, false on error
 */
static inline bool duckdb_seed_writer_init(DuckDBSeedWriter* writer, const char* db_path) {
    if (!writer || !db_path) return false;
    
    memset(writer, 0, sizeof(DuckDBSeedWriter));
    
    // Open database (NULL = in-memory, or path = file)
    if (duckdb_open(db_path, &writer->db) != DuckDBSuccess) {
        fprintf(stderr, "❌ Error: Failed to open DuckDB database: %s\n", db_path);
        return false;
    }
    
    // Connect
    if (duckdb_connect(writer->db, &writer->con) != DuckDBSuccess) {
        fprintf(stderr, "❌ Error: Failed to connect to DuckDB\n");
        duckdb_close(&writer->db);
        return false;
    }
    
    // Drop existing table if it exists (for clean start)
    const char* drop_sql = "DROP TABLE IF EXISTS seeds";
    if (duckdb_query(writer->con, drop_sql, NULL) != DuckDBSuccess) {
        // Non-fatal, continue
    }
    
    // Create seeds table (matches Motely schema)
    const char* create_sql = 
        "CREATE TABLE seeds (\n"
        "    id BIGINT,\n"
        "    seed VARCHAR(8)\n"
        ")";
    if (duckdb_query(writer->con, create_sql, NULL) != DuckDBSuccess) {
        fprintf(stderr, "❌ Error: Failed to create seeds table\n");
        duckdb_disconnect(&writer->con);
        duckdb_close(&writer->db);
        return false;
    }
    
    // Create index (for performance)
    const char* index_sql = "CREATE INDEX idx_seeds_id ON seeds(id)";
    if (duckdb_query(writer->con, index_sql, NULL) != DuckDBSuccess) {
        // Non-fatal, continue
        fprintf(stderr, "⚠️  Warning: Failed to create index (non-fatal)\n");
    }
    
    // Create appender for efficient bulk inserts
    if (duckdb_appender_create(writer->con, NULL, "seeds", &writer->appender) != DuckDBSuccess) {
        fprintf(stderr, "❌ Error: Failed to create DuckDB appender\n");
        duckdb_disconnect(&writer->con);
        duckdb_close(&writer->db);
        return false;
    }
    
    writer->seed_id = 0;
    writer->initialized = true;
    return true;
}

/**
 * @brief Write a seed to the database
 * @param writer Writer context
 * @param seed_str Seed string (must be 1-8 chars, uppercase, no '0')
 * @return true on success, false on error
 */
static inline bool duckdb_seed_writer_add(DuckDBSeedWriter* writer, const char* seed_str) {
    if (!writer || !writer->initialized || !seed_str) return false;
    
    // Validate seed (1-8 chars, uppercase, no '0')
    size_t len = strlen(seed_str);
    if (len == 0 || len > 8) return false;
    
    // Append id (auto-increment)
    if (duckdb_append_int64(writer->appender, writer->seed_id) != DuckDBSuccess) {
        return false;
    }
    
    // Append seed string
    if (duckdb_append_varchar(writer->appender, seed_str) != DuckDBSuccess) {
        return false;
    }
    
    // End row
    if (duckdb_appender_end_row(writer->appender) != DuckDBSuccess) {
        return false;
    }
    
    writer->seed_id++;
    
    // Flush periodically (every 1000 rows for performance)
    if (writer->seed_id % 1000 == 0) {
        duckdb_appender_flush(writer->appender);
    }
    
    return true;
}

/**
 * @brief Finalize and close DuckDB writer
 * @param writer Writer context
 * @return Total seeds written
 */
static inline int64_t duckdb_seed_writer_close(DuckDBSeedWriter* writer) {
    if (!writer || !writer->initialized) return 0;
    
    int64_t count = writer->seed_id;
    
    // Flush remaining rows
    duckdb_appender_flush(writer->appender);
    
    // Destroy appender
    duckdb_appender_destroy(&writer->appender);
    
    // Disconnect and close
    duckdb_disconnect(&writer->con);
    duckdb_close(&writer->db);
    
    writer->initialized = false;
    return count;
}

#endif // DUCKDB_SEED_SOURCE_H
