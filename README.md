# Yazıcı C# (Metin Asistanı)

Windows işletim sistemi için geliştirilmiş, global kısayollar ve yapay zeka desteği ile metin düzeltme/iyileştirme sağlayan bir masaüstü uygulamasıdır.

## Özellikler

- **🔠 Karakter Düzeltme**: Türkçe karakter hatalarını (deasciify) otomatik onarır.
- **🪄 AI İyileştirme**: Google Gemini API kullanarak metni dilbilgisi ve akıcılık açısından optimize eder.
- **🗣️ Sesle Yazma (STT)**: Mikrofonunuzdan aldığınız sesi anında metne çevirir.
- **🖱️ Yüzer Menü**: Herhangi bir uygulamada metin seçtiğinizde anında beliren hızlı işlem menüsü.
- **⚙️ Tray Entegrasyonu**: Sistem tepsisinden kolay erişim ve ayar yönetimi.

## Sistem Gereksinimleri

- Windows 10 / 11
- .NET 10 (veya .NET 8) Runtime

## Kurulum ve Çalıştırma

1. Projeyi bir terminalde açın.
2. Bağımlılıkları geri yükleyin ve çalıştırın:
   ```powershell
   dotnet run
   ```
3. Ayarlar için sistem tepsisindeki ikona sağ tıklayıp "Ayarlar"ı seçebilirsiniz.

## Yapılandırma

`.env` dosyasına Gemini API anahtarınızı eklemeyi unutmayın:
```text
GEMINI_API_KEY=your_api_key_here
```

---
*Bu proje orijinal Python versiyonundan C# diline taşınmıştır.*