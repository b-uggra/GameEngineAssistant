import time
from foundry_local_sdk import FoundryLocalManager
from foundry_local_sdk import configuration

print("🚀 Foundry Local Sunucusu Başlatılıyor...")

config = configuration.Configuration(app_name="GameEngineAssistant")
FoundryLocalManager.initialize(config)
manager = FoundryLocalManager.instance

MODELS_TO_LOAD = [
    "qwen3-embedding-0.6b-generic-gpu:1",
    "qwen2.5-coder-7b-instruct"
]

try:
    all_models = manager.catalog.list_models()
    
    for model_id in MODELS_TO_LOAD:
        print(f"\n🧠 '{model_id}' Modeli Aranıyor ve Yükleniyor...")
        loaded = False
        
        # Katalog nesnelerini tara
        for m in all_models:
            m_id = getattr(m, 'id', str(m))
            if model_id in m_id or m_id in model_id:
                print(f"✅ '{m_id}' kataloğunda bulundu, RAM'e yükleniyor...")
                if hasattr(m, 'download') and not getattr(m, 'is_cached', True):
                    m.download()
                if hasattr(m, 'load'):
                    m.load()
                    print(f"✅ '{m_id}' başarıyla RAM'e yüklendi!")
                    loaded = True
                    break

        # Katalog eşleşmesi doğrudan bulunamazsa alternatif yükleme yöntemi dene
        if not loaded:
            try:
                if hasattr(manager, 'load_model'):
                    manager.load_model(model_id)
                    print(f"✅ '{model_id}' doğrudan servis üzerinden yüklendi!")
                else:
                    print(f"⚠️ '{model_id}' katalogda eşleşmedi, ilk istek anında dinamik yüklenecek.")
            except Exception as load_err:
                print(f"⚠️ Otomatik yükleme uyarısı: {load_err}")

except Exception as e:
    print(f"ℹ️ Katalog tarama uyarısı: {e}")

print("\n🌐 Web servisi tetikleniyor...")
manager.start_web_service()

# Aktif portu tespit edip port.txt dosyasına yazıyoruz
try:
    active_url = manager.urls[0]
    port = active_url.split(":")[-1].replace("/", "")
    with open("port.txt", "w") as f:
        f.write(port)
    print(f"✅ Port otomatik senkronize edildi: {port}")
except Exception as ex:
    print(f"⚠️ Port dosyaya yazılamadı: {ex}")

print(f"\n✅ SUNUCU AKTİF! Dinlenen Adres(ler): {manager.urls}")

try:
    while True:
        time.sleep(1)
except KeyboardInterrupt:
    print("\nSunucu kapatılıyor...")
    manager.stop_web_service()