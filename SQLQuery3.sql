UPDATE ServiceOrders 
SET AccountingMonth = 4, AccountingYear = 2026 
WHERE EntryDate >= '2026-03-25 00:00:00' 
  AND EntryDate <= '2026-03-31 23:59:59';