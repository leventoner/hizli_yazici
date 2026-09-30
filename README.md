# Hızlı Yazıcı - C# Metin Asistanı

Windows işletim sistemi için geliştirilmiş, global kısayollar ve yapay zeka desteği ile metin düzeltme/iyileştirme sağlayan bir masaüstü uygulamasıdır.

## 📋 Özellikler

- **🔠 Karakter Düzeltme**: Türkçe karakter hatalarını (deasciify) otomatik onarır. Örneğin `guzel` → `güzel`, `sirket` → `şirket` gibi.
- **🪄 AI İyileştirme**: Google Gemini API kullanarak metni dilbilgisi ve akıcılık açısından optimize eder.
- **🗣️ Sesle Yazma (STT)**: Mikrofonunuzdan aldığınız sesi anında metne çevirir.
- **🖱️ Yüzer Menü**: Herhangi bir uygulamada metin seçtiğinizde anında beliren hızlı işlem menüsü.
- **⚙️ Tray Entegrasyonu**: Sistem tepsisinden kolay erişim ve ayar yönetimi.

## 🖥️ Sistem Gereksinimleri

- **İşletim Sistemi**: Windows 10 / Windows 11
- **Runtime**: .NET 8 Runtime (veya self-contained build ile runtime gerekmez)

## 🚀 Kurulum ve Çalıştırma

### 1. Projeyi Çalıştırma (Geliştirme Modu)

Projeyi bir terminalde açın ve şu komutu çalıştırın:

```powershell
cd c:\Users\lener\Desktop\code\csharp\hizli_yazici
dotnet run
```

### 2. EXE Dosyası Oluşturma

#### Seçenek A: Self-Contained EXE (Önerilen - Runtime gerekmez)

Bu yöntem .NET Runtime kurulu olmayan bilgisayarlarda da çalışır:

```powershell
dotnet restore --force
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
```

> **Not:** İlk çalıştırmada `dotnet restore --force` komutu nuget.config'tan en son runtime paketlerini çeker.

Bu komut `publish/` klasöründe çalıştırılabilir exe dosyalarını oluşturur (~145 MB).

#### Seçenek B: Framework-Dependent EXE (Daha küçük boyut)

Sadece .NET 8 Runtime kurulu bilgisayarlarda çalışır, daha küçük boyutludur:

```powershell
dotnet publish -c Release -o ./publish
```

#### Seçenek C: Basit Build

```powershell
dotnet build -c Release
```

Bu komut `bin/Release/net8.0-windows/` klasöründe dll ve exe oluşturur.

### 3. Oluşturulan EXE'yi Çalıştırma

1. `publish/HizliYazici.exe` dosyasını çalıştırın.
2. Uygulama sistem tepsisinde (sağ alt köşe) bir simge ile görünür.
3. Herhangi bir metin seçtiğinizde yüzer menü belirir.

## ⚙️ Yapılandırma

### API Anahtarı Ayarlama

`.env` dosyasına Gemini API anahtarınızı eklemeyi unutmayın:

```text
GEMINI_API_KEY=your_api_key_here
```

> Not: `.env.example` dosyasını `.env` olarak kopyalayıp API anahtarınızı girin.

### Sistem Tepsisi

Uygulama sistem tepsisinde (sağ alt köşe) bir simge ile çalışır. Ayarlar için sistem tepsisindeki ikona sağ tıklayıp "Ayarlar"ı seçebilirsiniz.

### Ayar Dosyası (settings.json)

Ayarlar, uygulamanın çalıştığı klasörde otomatik olarak `settings.json` dosyasına kaydedilir. Önemli varsayılanlar:

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| `SttHotkey` | `ctrl+shift+y` | Sesle yazma (STT) global kısayolu |
| `SttDuration` | `10` | Kayıt süresi (saniye) |
| `FloatingMenuDuration` | `10` | Yüzer menünün görünür kalma süresi (saniye) |
| `ColorTheme` | `#3d5afe` | Yüzer menü renk teması |

## 📁 Proje Yapısı

```
hizli_yazici/
├── App.xaml                    # WPF Application tanımı
├── App.xaml.cs                  # Uygulama giriş noktası
├── AssemblyInfo.cs              # Assembly metadata
├── YaziciCS.csproj              # Proje dosyası
├── nuget.config                 # NuGet yapılandırması
├── .env.example                 # Environment değişkenleri şablonu
├── icon.ico                     # Uygulama ikonu
├── dummy.cs                     # UIAutomation referans testi (geçici yardımcı)
│
├── Managers/                    # Yönetici sınıfları
│   ├── ActionHandler.cs         # Eylem işleyici
│   ├── GlobalHotkeyManager.cs   # Global kısayol yöneticisi
│   ├── GlobalKeyboardHook.cs    # Klavye dinleyici
│   ├── GlobalMouseHook.cs       # Fare dinleyici
│   ├── SelectionManager.cs      # Metin seçim yöneticisi
│   └── SettingsManager.cs       # Ayarlar yöneticisi
│
├── Services/                    # Servis sınıfları
│   ├── AudioService.cs          # Ses kayıt servisi (STT)
│   ├── Deasciifier.cs           # Türkçe karakter düzeltici
│   ├── TextService.cs           # Metin işleme servisi
│   └── TurkishPatternTable.cs   # Türkçe karakter kalıpları
│
├── UI/                          # Kullanıcı arayüzü
│   ├── FloatingMenu.xaml        # Yüzer menü tasarımı
│   ├── FloatingMenu.xaml.cs     # Yüzer menü kodları
│   ├── NotificationWindow.xaml # Bildirim penceresi
│   ├── NotificationWindow.xaml.cs
│   ├── SettingsWindow.xaml      # Ayarlar penceresi
│   └── SettingsWindow.xaml.cs
```

> **Not:** `publish/` klasörü yalnızca `dotnet publish` çalıştırıldığında oluşturulur ve versiyon takibi yapılmaz.

## 🔧 Teknolojiler

- **Framework**: .NET 8 / WPF
- **Programlama Dili**: C#
- **AI Servisi**: Google Gemini API (metin iyileştirme ve ses tanıma için kullanılır)
- **Ses Kaydı**: Windows `winmm.dll` (mciSendString) ile mikrofon kaydı; ses dosyası transkripsiyon için Gemini API'ye gönderilir

## 📝 Lisans

Bu proje eğitim amaçlı geliştirilmiştir.
