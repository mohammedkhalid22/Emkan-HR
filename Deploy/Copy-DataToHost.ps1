# Copies every row from the local HR database to the hosted one, so the online copy
# has the same employees, accounts and history.
# The hosted database must already have the tables: open the site once first
# (the app creates them on start), then run this script.
# WARNING: it empties the hosted database before copying.

param(
    [string]$Target,
    [string]$Source = "Server=.\MSSQLSERVER_2022;Database=HRSystemDb;Trusted_Connection=True;TrustServerCertificate=True"
)

$ErrorActionPreference = "Stop"

if (-not $Target) {
    $Target = Read-Host "Paste the hosted database connection string"
}

function Invoke-Sql($conn, $sql) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 300
    [void]$cmd.ExecuteNonQuery()
}

$src = New-Object System.Data.SqlClient.SqlConnection $Source
$dst = New-Object System.Data.SqlClient.SqlConnection $Target
$src.Open()
$dst.Open()

try {
    $cmd = $src.CreateCommand()
    $cmd.CommandText = "SELECT QUOTENAME(s.name) + '.' + QUOTENAME(t.name) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.name <> '__EFMigrationsHistory' ORDER BY t.name"
    $reader = $cmd.ExecuteReader()
    $tables = @()
    while ($reader.Read()) { $tables += $reader.GetString(0) }
    $reader.Close()

    Write-Host "Found $($tables.Count) tables."

    foreach ($t in $tables) { Invoke-Sql $dst "ALTER TABLE $t NOCHECK CONSTRAINT ALL" }
    foreach ($t in $tables) { Invoke-Sql $dst "DELETE FROM $t" }

    foreach ($t in $tables) {
        $cmd = $src.CreateCommand()
        $cmd.CommandText = "SELECT * FROM $t"
        # Load into a DataTable first: copying straight from the reader fails when the two
        # databases use different collations (local Arabic vs the host's Latin default)
        $table = New-Object System.Data.DataTable
        $reader = $cmd.ExecuteReader()
        $table.Load($reader)
        $reader.Close()

        $bulk = New-Object System.Data.SqlClient.SqlBulkCopy($dst, ([System.Data.SqlClient.SqlBulkCopyOptions]::KeepIdentity -bor [System.Data.SqlClient.SqlBulkCopyOptions]::KeepNulls), $null)
        $bulk.DestinationTableName = $t
        $bulk.BulkCopyTimeout = 600
        foreach ($col in $table.Columns) {
            [void]$bulk.ColumnMappings.Add($col.ColumnName, $col.ColumnName)
        }
        $bulk.WriteToServer($table)

        $count = $dst.CreateCommand()
        $count.CommandText = "SELECT COUNT(*) FROM $t"
        Write-Host ("{0,-40} {1,6} rows" -f $t, $count.ExecuteScalar())
    }

    foreach ($t in $tables) { Invoke-Sql $dst "ALTER TABLE $t WITH CHECK CHECK CONSTRAINT ALL" }
    Write-Host "Done. All data copied." -ForegroundColor Green
}
finally {
    $src.Close()
    $dst.Close()
}
