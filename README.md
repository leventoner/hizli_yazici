# Hızlı Yazıcı

Hızlı Yazıcı, Windows'ta seçtiğiniz metni düzeltmenize, iyileştirmenize veya çevirmenize yardımcı olan bir masaüstü uygulamasıdır. Uygulama sistem tepsisinde çalışır; seçili metnin yanında açılan küçük menüden işlem seçebilir veya klavye kısayollarını kullanabilirsiniz.

## Özellikler

- **Türkçe karakter düzeltme:** `guzel`, `sirket` gibi Türkçe karakterleri eksik yazılmış kelimeleri yerel olarak düzeltmeye çalışır. Bu özellik için internet bağlantısı veya API anahtarı gerekmez.
- **Metin iyileştirme:** Gemini metnin dilini koruyarak yazım, noktalama, dilbilgisi ve akıcılık üzerinde çalışır. Metin Gemini API'ye gönderilir.
- **Çeviri:** Türkçe metni İngilizceye, diğer dillerdeki metni Türkçeye çevirir. Varsayılan ücretsiz mod önce Google'ın anahtarsız çeviri uç noktasını, yanıt alınamazsa MyMemory servisini dener. İsterseniz ayarlardan Gemini sağlayıcısını seçebilirsiniz. Kaynak metin seçilen sağlayıcıya gönderilir.
- **Sözlük (tek kelime):** Çeviri için **tek bir kelime** seçtiğinizde otomatik olarak sözlük moduna geçilir. Kelime Türkçe veya yabancı olabilir; Gemini varsa kök (lemma) analizi, ek/dilbilgisi notu, anlam(lar), örnek cümleler ve eş anlamlılar üretilir. Gemini anahtarı yoksa ücretsiz ek analizi + ücretsiz çeviri ile temel bir giriş gösterilir. Sonuç, seçilebilir ve kopyalanabilir bir pencerede sunulur.
- **Sesle yazma:** Mikrofon kaydını Gemini'ye göndererek metne dönüştürür. Kayıt süresi ve global kısayol ayarlanabilir. Ses işleme için Gemini API anahtarı ve mikrofon erişimi gerekir.
- **Yüzer menü ve tepsi:** Metin seçtiğinizde düzeltme, iyileştirme ve çeviri seçenekleri sunan menü açılır. Uygulama tepsi simgesinden ayarlanabilir veya kapatılabilir; ana pencere kapalıyken de tepside çalışmaya devam eder.

## Gereksinimler

- Windows 10 veya Windows 11
- Geliştirme ve derleme için .NET 8 SDK
- Framework-dependent yayımlanan uygulamayı çalıştırmak için .NET 8 Desktop Runtime
- Self-contained yayımlanan uygulama için ayrıca .NET Runtime kurulumu gerekmez (Windows x64 paketi)

Proje WPF kullanır ve `net8.0-windows` hedefini derler. Self-contained paket yalnızca hedeflediğiniz mimari için kullanılabilir; aşağıdaki örnek Windows x64 içindir.

## Geliştirme Modunda Çalıştırma

PowerShell'de proje klasörüne geçin. Kendi bilgisayarınızdaki yolu kullanın:

```powershell
cd C:\path\to\hizli_yazici
dotnet restore
dotnet run
```

`dotnet restore`, NuGet bağımlılıklarını indirir; `dotnet run` projeyi derleyip başlatır. Uygulama açıldığında ana pencere yerine sistem tepsisinde çalışır. Uygulamadan çıkmak için tepsi simgesine sağ tıklayıp **Çıkış** seçin.

## Derleme ve Yayımlama

Komutları proje dosyasının (`YaziciCS.csproj`) bulunduğu klasörde çalıştırın. İlk derlemede gerekli paketler otomatik geri yüklenir; ayrıca `dotnet restore` çalıştırmak da mümkündür.

### Release derlemesi

```powershell
dotnet build -c Release
```

Bu, uygulamayı Release yapılandırmasında derler. Çıktı `bin\Release\net8.0-windows\` altındadır. Bu klasördeki uygulama .NET Desktop Runtime gerektirir; başkasına dağıtmak için aşağıdaki publish seçeneklerinden birini tercih edin.

### Framework-dependent publish

Daha küçük bir dağıtım üretir, ancak hedef bilgisayarda .NET 8 Desktop Runtime kurulu olmalıdır:

```powershell
dotnet publish -c Release -o .\publish\framework-dependent
```

Uygulamayı `publish\framework-dependent\HizliYazici.exe` ile başlatın. Dağıtırken yalnızca `.exe` dosyasını değil, bu çıktı klasörünün **tamamını** paylaşın.

### Self-contained Windows x64 publish

Hedef bilgisayara ayrıca .NET Runtime kurmadan çalıştırmak için:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o .\publish\win-x64
```

Bu seçenek .NET çalışma zamanını da pakete eklediğinden daha büyük çıktı üretir; boyut kullanılan SDK ve bağımlılık sürümlerine göre değişir. Uygulamayı `publish\win-x64\HizliYazici.exe` ile başlatın ve dağıtırken `publish\win-x64\` klasörünün **tamamını** paylaşın. Bu paket Windows x64 içindir.

İki publish seçeneği farklı klasörlere çıktı verir; böylece önceki paketten kalan dosyalar yeni çıktıyla karışmaz. `publish/`, `bin/` ve `obj/` klasörleri Git tarafından takip edilmez.

## Yapılandırma

### Gemini API anahtarı

Gemini API anahtarı; metin iyileştirme, sesle yazma ve Gemini ile çeviri için gereklidir. Varsayılan ücretsiz çeviri ve yerel Türkçe karakter düzeltme için anahtar gerekmez.

Uygulamanın çalıştığı klasöre `.env` adlı bir metin dosyası oluşturup anahtarı şu biçimde ekleyin:

```text
GEMINI_API_KEY=anahtariniz
```

Geliştirme sırasında `.env` dosyasını proje köküne koyabilirsiniz. Yayımlanmış uygulamada en kolay yöntem dosyayı `HizliYazici.exe` ile aynı klasöre koymaktır. `.env` dosyası gizli bilgi içerdiğinden kaynak kontrolüne eklemeyin.

### Ayarlar

Ayarlar penceresine tepsi menüsünden ulaşabilirsiniz. Değerler uygulamanın çalıştığı klasördeki `settings.json` dosyasına kaydedilir; dosya ilk çalıştırmada yoksa varsayılanlarla oluşturulur. JSON'daki alan adları küçük harfli ve alt çizgilidir:

| JSON alanı | Varsayılan | Açıklama |
|---|---|---|
| `stt_hotkey` | `ctrl+shift+y` | Sesle yazma global kısayolu |
| `stt_duration` | `10` | Mikrofon kayıt süresi (saniye) |
| `cooldown` | `0.5` | Ctrl+C ile tetiklenen çoklu tıklama algılama aralığı (saniye) |
| `enable_character_fix` | `true` | Karakter düzeltme özelliği |
| `enable_ai_improve` | `true` | AI ile metin iyileştirme özelliği |
| `enable_speech_to_text` | `true` | Sesle yazma özelliği |
| `notify_on_no_change` | `true` | Düzeltme değişiklik yapmadığında bildirim gösterme |
| `floating_menu_duration` | `10` | Yüzer menünün ekranda kalma süresi (saniye) |
| `color_theme` | `#3d5afe` | Menü ve bildirimlerde kullanılan tema rengi |
| `translation_provider` | `free` | `free` veya `gemini`; Gemini seçeneği API anahtarı gerektirir |
| `translation_email` | boş | MyMemory için isteğe bağlı e-posta adresi |

Ücretsiz çeviri sağlayıcısı metnin dilini otomatik algılamayı dener ve hedef dili Türkçe/İngilizce olarak belirler. Google isteği başarısız olursa MyMemory yedek olarak kullanılır. Ayarlara bir e-posta girilmesi MyMemory'nin anonim kullanım limitini artırabilir; bu bilgi MyMemory'nin kendi limitlerine tabidir.

## Proje Yapısı

```text
hizli_yazici/
├── App.xaml, App.xaml.cs       # WPF uygulaması ve başlangıç akışı
├── YaziciCS.csproj             # .NET hedefi ve proje ayarları
├── Managers/                   # Kısayollar, seçim, ayarlar ve eylem yönetimi
├── Services/                   # Metin, ses ve çeviri servisleri
└── UI/                         # Yüzer menü, bildirim ve ayarlar pencereleri
```

## Teknolojiler

- C#, .NET 8 ve WPF
- Google Gemini API: metin iyileştirme, ses transkripsiyonu ve isteğe bağlı çeviri
- Ücretsiz çeviri: Google'ın anahtarsız çeviri uç noktası ve MyMemory yedeği
- Ses kaydı: Windows `winmm.dll` (`mciSendString`)

## Lisans

Bu proje eğitim amaçlı geliştirilmiştir.