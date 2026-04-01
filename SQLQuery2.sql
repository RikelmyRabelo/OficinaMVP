UPDATE ServiceOrders 
SET CompletionDate = EntryDate 
WHERE CAST(CompletionDate AS DATE) = '2026-04-01' AND Status = 'Completed';