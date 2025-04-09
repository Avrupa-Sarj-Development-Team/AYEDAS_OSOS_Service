CREATE TABLE IF NOT EXISTS "MeterOsosConsumption" (
    "Id" SERIAL PRIMARY KEY,
    "Period" VARCHAR(50),
    "Etso" VARCHAR(50),
    "MeterId" INTEGER,
    "DistributionCompany" VARCHAR(50) DEFAULT 'AYEDAS',
    "Year" INTEGER,
    "Month" INTEGER,
    "Day" INTEGER,
    "Hour" INTEGER,
    "DateTime" TIMESTAMP,
    "Value" NUMERIC(15,5),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_consumption_data UNIQUE ("Etso", "Year", "Month", "Day", "Hour")
); 