# MeshCentral Server Analizi

## Genel Bakis
- MeshCentral/Paket-2025-10-31/MeshCentral dizini Node.js tabanli tam fonksiyonel uzak cihaz yonetim sunucusunu barindirir; giris noktasi `MeshCentral/meshcentral.js`.
- Sunucu ayni anda HTTPS web arayuzu, agent baglantilari, Intel AMT CIRA tünelleri, MQTT ve mesajlasma gibi farkli servisleri ayaga kaldirir.
- Varsayilan dagitim Windows, Linux ve macOS uzerinde calisir; Windows servis entegrasyonu `node-windows` ile saglanir.

## Teknoloji Yigini
- Cekirdek dil: ECMAScript 6 uyumlu Node.js (`package.json` > `engines.node >= 16`).
- Web katmani: `express`, `express-handlebars`, `express-ws`, `body-parser`, `compression`.
- Gercek zamanli iletisim: `ws` (WebSocket), Intel AMT icin TLS soketleri, ic ag icin mesh relay.
- Sertifika ve sifreleme: `node-forge`, dahili `certoperations.js`, opsiyonel Let's Encrypt (`letsencrypt.js`).
- Kimlik dogrulama: `cookie-session`, `otplib` (TOTP), `webauthn.js` (FIDO2), istege bagli SSPI (`webserver.js`).
- Veritabani arabirimi: `db.js` uzerinden NeDB (`@seald-io/nedb`) varsayilan; parametrelerle MongoDB, MariaDB, MySQL, PostgreSQL, AceBase, SQLite desteklenir.

## Cekirdek Sunucu Bilesenleri (`meshcentral.js`)
- `CreateMeshCentralServer` fonksiyonu tum servisleri olusturur ve konfiguruasyon okur.
- `webserver`: HTTPS ana arayuz (`webserver.js`), Handlebars sablonlari `views/` ve frontend scriptleri `public/` araciligiyla sunar.
- `redirserver`: HTTP isteklerini HTTPS'e yonlendirir (`apprelays.js`).
- `mpsserver`: Intel AMT Management Presence Server (`mpsserver.js`) TLS/SSH benzeri APF protokolunu uygular.
- `mqttbroker`: Opsiyonel MQTT broker (`mqttbroker.js`) olay dagitimi icin.
- `meshrelay`: Web tabanli uzak masaustu/veri tunneli katmani (`meshrelay.js`, `meshdesktopmultiplex.js`, `meshdevicefile.js`).
- `meshagent`: Agent baglantilarini yoneten modul (`meshagent.js`) ve `agents/` altindaki platform bagimli ikilileri gunceller.
- `amtManager`, `amtScanner`: Intel AMT cihaz aktivasyonu ve politikalari (`amtmanager.js`, `amtscanner.js`).
- `meshBot`, `taskmanager`, `meshaccelerator`: Otomasyon, zamanlama ve performans uzmanlastirmalari icin.
- `pluginHandler`: `meshcentral-data/plugins` altindan moduller yukler, agent corelarina ekstra kod ekleyebilir (`pluginHandler.js`).

## Veri ve Durum Yonetimi (`db.js`)
- Konfigurasyon ve cihaz durumlari tek `CreateDB` arabirimi ile erisilebilir.
- Veritabanlari icin otomatik bakim: etkinlik ve guc loglari icin zaman asimi, `maintenance()` ile periyodik temizleme.
- Sunucu, coklu kopya senaryolari icin benzersiz `DatabaseIdentifier` olusturur ve peer modunda uyum kontrolu yapar.
- Sifrelenmis kayit destegi: `dbRecordsEncryptKey` alanlari ile opsiyonel veri sifreleme.

## Iletisim Kanallari ve Protokoller
- Web arayuzu: HTTPS + WebSocket (agent komutlari ve bildirimler).
- Mesh agent tünelleri: TLS uzerinden ikili protokol (komut, dosya, terminal, masaustu multiplex).
- Intel AMT: APF/SSH tabanli CIRA kanal yonetimi, keep-alive, kanal acma/kapama (`mpsserver.js`).
- Relay ve paylasim: `webrelayserver.js` ve `meshrelay.js` araciligiyla konuk erisimi, port yönlendirme.
- Mesajlasma/SMS: `messaging.js`, `meshsms.js` ile SMS gateway ve ic mesajlar.

## Guvenlik Mekanizmalari
- TLS sertifika yonetimi: Varsayilan self-signed, ozellestirilmis veya Let's Encrypt (`letsencrypt.js`).
- Cok faktorlu kimlik dogrulama: TOTP (`otplib`), WebAuthn, eposta/sms tokenleri.
- IP tabanli filtrasyon: `settings.userallowedip`, `agentallowedip`, `agentblockedip` (`webserver.js`).
- Oturum guvenligi: `cookieUseOnceTable` ile bir kere kullanilabilir cookie, `loginCookieEncryptionKey` ile sifrelenmis oturum tokenleri.
- Yetki modeli: Mesh ve site haklari bitmask olarak tanimlanir (ornegin `MESHRIGHT_REMOTECONTROL`, `SITERIGHT_ADMIN`).
- Audit ve loglama: `mesherrors.txt`, `events` koleksiyonlari, opsiyonel `authLog` cagrilari.

## Konfigurasyon ve Ozellestirme
- `meshcentral-config-schema.json` JSON Schema ile `config.json` yapisini dogrular.
- Ornekler `sample-config.json` ve `sample-config-advanced.json` dosyalarinda.
- Ayarlar CLI argumanlari, ortam degiskenleri (`meshcentral_*`) veya config uzerinden birlestirilir (`meshcentral.js`).
- Coklu domain destegi: `domains` nesnesi ile logo, eposta, sifre politikasi gibi domain bazli ozellikler.
- `plugins` bolumu ile eklentiler otomatik indirilebilir veya manuel yuklenebilir.

## Eklenti ve Core Genisletme Mimari
- `pluginHandler.js` eklentileri `meshcentral-data/plugins/<plugin>` dizininden yukler.
- Her plugin `exports` araciligiyla UI veya sunucu hook'larina baglanir; agent core koduna ek JS modulleri itebilir (`modules_meshcore`).
- `callHook` altyapisi `onServerStart`, `onWebUIStartupEnd` gibi olaylara abone olma imkani sunar.

## Isleyis Akisi (Lifecycle)
1. `meshcentral.js` konfigurasyonu okur, veri ve dosya dizinlerini dogrular (`meshcentral-data`, `meshcentral-files`, `meshcentral-backups`).
2. Sertifika seti olusturulur veya yuklenir (`certoperations.js`).
3. Veritabani baglantisi acilir, schema versiyonu kontrol edilir (`db.js`).
4. Ana servisler (web, redir, mps, mqtt, swarm) portlara baglanir; plugin ve mesh core guncellemeleri tetiklenir.
5. Arka plan timer'lari (`maintenanceTimer`, `taskLimiter`) bakim ve gorev dagilimlarini yonetir.
6. Agent ve tarayicilar baglandikca `SetConnectivityState` ile durum tablolarina kaydedilir.

## Onemli Dosya ve Klasorler
- `meshcentral.js`: sunucu baslatma ve servis kompozisyonu.
- `webserver.js`: Express tabanli web arayuzu, REST/WebSocket endpointleri, sertifika hash hesaplama.
- `mpsserver.js`: Intel AMT CIRA isleme.
- `db.js`: Tüm veri katmani ve bakim.
- `meshrelay.js`, `meshdesktopmultiplex.js`, `meshdevicefile.js`: uzak masaustu, dosya ve terminal akislari.
- `views/`, `public/`, `translate/`: UI sablonlari, statik dosyalar ve lokalizasyon.
- `agents/`: platform bagimli agent paketleri ve skriptleri.
- `emails/`: sablon eposta icerikleri.
- `meshcentral-data/`: calisma zamanli ayarlar, sertifikalar, veritabani dump'lari.

## Platform ve Dagitim Ozellikleri
- Windows servis entegrasyonu: `node-windows` uzerinden `meshcentral --install` komutu ve `winservice.js` entegrasyonu.
- Linux/macOS: systemd veya manuel servis scriptleri icin `CreateSourcePackage.bat` ve `docker/` ornekleri mevcut.
- Docker destek dosyalari `docker/` klasorunde docker-compose ve Dockerfile ornekleri saglar.

## Surumleme ve Guncelleme
- Sunucu surumu `package.json` uzerinden okunur (`meshcentral.js` > `getCurrentVersion`).
- Agent guncellemeleri icin `meshAgentBinaries` ve blok boyutu (`agentUpdateBlockSize`) takip edilir.
- `CreateSourcePackage.bat` ve `reinstall-modules.bat` dagitim ve gelistirme sureclerini kolaylastirir.

## Gelistirme ve Dokumantasyon Kaynaklari
- Resmi dokuman `docs/` ve `docs/docs` altinda Markdown olarak tutulur, MkDocs ile yayinlanabilir (`docs/mkdocs.yml`).
- Tasarim/Architecture PDF baglantilari `readme.md` icinde mevcuttur ve sunucu ic yapisi, agent el sikismasi gibi konulari ayrintilar.
