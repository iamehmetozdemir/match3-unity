# Match-3 Puzzle (Unity)

[English](#english) · [Türkçe](#türkçe)

**Play in your browser:** https://iamehmetozdemir.github.io/match3-unity/

<p align="center">
  <img src="docs/gameplay.jpg" alt="Gameplay" width="320">
  &nbsp;
  <img src="docs/specials.png" alt="Special tiles" width="320">
</p>

## English

A match-3 puzzle game I made in Unity 6 with C#. You swap neighbouring tiles to make lines of three or more, and you have to reach the target score before you run out of moves. There are five levels.

- Bigger matches make special tiles: 4 in a row makes a rocket (clears a row or column), an L or T shape makes a bomb (3x3 area), and 5 in a row makes a color bomb (clears one color). Specials can trigger each other.
- Chain reactions score more with every step.
- If no move is left, the board shuffles. If you stay idle for a few seconds, it shows a hint.
- There are no art files, all sprites are drawn in code. Every color also has its own shape, so it's readable if you can't tell colors apart.

### How the code is organized

The game rules live in `Assets/Scripts/Core` and don't use anything from Unity. `MatchEngine.TrySwap` plays out a whole turn at once and returns a plain description of what happened (what was cleared, what fell, what was refilled, for each step of the chain). The Unity side (`Assets/Scripts/Game`) just replays that as animation. This made the rules easy to test without opening Unity, and I could change them without touching the animation code.

### Tests

- 26 unit tests for the rules (matches, L/T shapes, each special tile, chains, gravity, scoring, shuffling, win/lose).
- 3 Play Mode tests that start the real game, play 25 turns, and check that what's on screen matches the board.
- The rules tests also run without Unity: `dotnet test Match3.Core.Tests`.

I set the score targets by letting a random-move bot and a "best immediate move" bot play each level 300 times, and putting the target between their scores. No human has playtested the difficulty yet.

### Run it

You need Unity 6 (I used 6000.6.6f1). Add the folder in Unity Hub, open it and press Play. There's no scene to set up, the game starts itself from code. **Match3 → Build WebGL** in the menu makes the browser version.

Controls: drag a tile towards a neighbour, or click two neighbouring tiles.

### Not done / ideas

- No sound.
- The HUD uses Unity's old IMGUI. A proper UI would look better.
- Only a score goal. Real match-3 games have things like collect goals and obstacles.
- I haven't tried it on a phone. Touch should work through Unity's mouse emulation, but I haven't checked.

## Türkçe

Unity 6 ve C# ile yaptığım bir match-3 bulmaca oyunu. Yan yana taşları değiştirip üçlü ya da daha uzun sıralar yapıyorsun ve hamlelerin bitmeden hedef skora ulaşman gerekiyor. Beş level var.

- Büyük eşleşmeler özel taş yapıyor: 4'lü sıra roket (satır ya da sütunu temizler), L veya T şekli bomba (3x3 alan), 5'li sıra renk bombası (bir rengin hepsini temizler). Özel taşlar birbirini tetikleyebiliyor.
- Zincirleme patlatmalar her adımda daha çok puan veriyor.
- Hamle kalmazsa tahta karışıyor. Birkaç saniye hareketsiz kalırsan ipucu gösteriyor.
- Hiç görsel dosya yok, tüm sprite'lar kodla çizildi. Her rengin kendi şekli de var, renkleri ayıramasan da okunuyor.

### Kod nasıl düzenlendi

Oyun kuralları `Assets/Scripts/Core` içinde ve Unity'den hiçbir şey kullanmıyor. `MatchEngine.TrySwap` bir hamlenin hepsini bir anda oynayıp ne olduğunu düz veri olarak veriyor (ne temizlendi, ne düştü, ne yeniden doldu, zincirin her adımı için). Unity tarafı (`Assets/Scripts/Game`) bunu sadece animasyon olarak tekrar oynatıyor. Böylece kuralları Unity'yi açmadan test etmek kolay oldu ve animasyon koduna dokunmadan değiştirebildim.

### Testler

- Kurallar için 26 birim test (eşleşmeler, L/T şekilleri, her özel taş, zincirler, yerçekimi, puan, karıştırma, kazanma/kaybetme).
- Gerçek oyunu başlatıp 25 hamle oynayan ve ekrandakinin tahtayla aynı olduğunu kontrol eden 3 Play Mode testi.
- Kural testleri Unity olmadan da çalışıyor: `dotnet test Match3.Core.Tests`.

Hedef skorları, rastgele oynayan bir bot ile "en iyi anlık hamleyi seçen" bir botun her level'ı 300'er kez oynamasıyla, hedefi ikisinin skoru arasına koyarak ayarladım. Zorluğu henüz bir insan denemedi.

### Çalıştırma

Unity 6 lazım (ben 6000.6.6f1 kullandım). Klasörü Unity Hub'da ekle, aç ve Play'e bas. Kurulacak sahne yok, oyun kendini koddan başlatıyor. Menüdeki **Match3 → Build WebGL** tarayıcı sürümünü çıkarıyor.

Kontroller: taşı komşusuna doğru sürükle ya da yan yana iki taşa tıkla.

### Yapılmayanlar / fikirler

- Ses yok.
- HUD Unity'nin eski IMGUI'siyle yapıldı. Düzgün bir arayüz daha güzel olurdu.
- Sadece skor hedefi var. Gerçek match-3 oyunlarında topla-hedefi, engel gibi şeyler de olur.
- Telefonda denemedim. Dokunma, Unity'nin fare emülasyonuyla çalışmalı ama kontrol etmedim.
