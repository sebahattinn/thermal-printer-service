# UI Ekran Görüntüleri

Operatör paneli (`http://localhost:8080/`) için screenshot rehberi.

| Dosya adı | Ne göstermeli | Senaryo |
|---|---|---|
| `01-dashboard-overview.png` | Tüm dashboard (Mock bağlı, en az 1-2 başarılı basım sonrası) | Başlık badge'leri yeşil/mor; status kartında pending=0, failed=0; loglar tablosunda satırlar; rulo ömrü %99+ |
| `02-print-receipt-flow.png` | ACO örnek fişi basıldıktan sonraki an | "ACO örnek fişi bas" butonuna basılmış, başarılı banner görünür, son iş "Raw • Succeeded" |
| `03-failed-job-reprint.png` (opsiyonel) | Bağlantı yokken oluşan failed job + Tekrar Bastır butonu | Mock öncesi 1 basım yap → failed olur → "5. Başarısız işler" kartında jobId + Tekrar Bastır butonu görünür |
| `04-paper-roll-tracking.png` (opsiyonel) | Rulo ömrü kartı çalışırken | Birkaç basım sonrası kalan mm, %, tahmini fiş, ortalama mm/iş dolmuş |

PNG formatında, en az 1280px geniş, kompresyon kabul edilir.

## Nasıl alınır (Windows 10/11)

1. **API'yi başlat** — yeni PowerShell aç:
   ```powershell
   cd "C:\Users\sebahattin\Desktop\001_Sebahattin_TEĞİ_AcoRecycling\ThermalPrinterService"
   dotnet run --project src/ThermalPrinterService.Api
   ```
2. **Tarayıcı**: `http://localhost:8080/`
3. **`Win + Shift + S`** → ekran kesme aracı açılır → tarayıcı penceresinin tamamını seç → otomatik panoya kopyalanır
4. **Paint** aç (`Win` → "paint" yaz) → `Ctrl + V` → `Dosya > Farklı Kaydet > PNG` → bu klasöre (`docs/screenshots/`) yukarıdaki isimle kaydet

VEYA daha kolay: `Win + PrintScreen` direkt PNG olarak `Pictures/Screenshots/` altına atar; sonra buraya kopyalarsın.

## Şu PNG'lerin senaryosu (adım adım)

### `01-dashboard-overview.png`
1. UI'ı aç (`http://localhost:8080/`)
2. "1. Bağlantı" → `Mock` seçili → **Bağlan**
3. "3. Hızlı işlem" → **`Test fişi bas (metin)`** — 1-2 kez
4. 5 saniye bekle, badge'ler yeşil olsun, loglar dolsun
5. SS al

### `02-print-receipt-flow.png`
1. `01`'den sonra "3. Hızlı işlem" → **`ACO örnek fişi bas`**
2. Üst banner "Fiş kuyruğa atıldı" yeşil görünür, son iş = `Raw • Succeeded`
3. SS al

### `03-failed-job-reprint.png` (opsiyonel)
1. API'yi yeni başlat (bağlanmadan!)
2. Doğrudan "Metin" sekmesi → "test" yaz → **Bas**
3. Worker bağlantısız olduğu için failed işaretler
4. "5. Başarısız işler" kartında jobId + **Tekrar Bastır** butonu görünür
5. SS al

### `04-paper-roll-tracking.png` (opsiyonel)
1. Mock'la bağlan, 5-10 fiş bas
2. "Rulo ömrü" kartında progress bar ve sayılar (kalan mm, ortalama mm/iş, tahmini fiş) dolu
3. SS al
