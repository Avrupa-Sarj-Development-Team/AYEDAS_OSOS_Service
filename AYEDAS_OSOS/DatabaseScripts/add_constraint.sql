-- Tablomuza unique constraint ekleme
ALTER TABLE "MeterOsosConsumption" 
ADD CONSTRAINT unique_consumption_data 
UNIQUE ("Etso", "Year", "Month", "Day", "Hour"); 