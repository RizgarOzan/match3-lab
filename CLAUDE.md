# Match3 Lab — proje kurallari

Match-3 **tezgahi**, oyun degil: motordan bagimsiz kural cekirdegi + diff'te okunabilir seviye formati + bir seviyenin zorlugunu insan oynamadan olcen bot simulatoru. .NET 10 SDK; Unity tarafi Unity 6000.3 + WebGL modulu.

## Mimari sinir — en onemli kural
`packages/com.rizgarozan.match3lab.core/` **saf C#'tir ve `UnityEngine`'e referans veremez.** Tek dogruluk kaynagi burasidir; `unity/` sadece sunumdur (tahta gorseli, Level Editor, Difficulty Curve pencereleri, play-mode testleri). Cekirdege `using UnityEngine` girerse CLI ve testler olur, simulator calismaz — bu siniri korumak her seyden onemli.

Katmanlar: `packages/.../core` (kurallar, simulasyon) → `tools/Match3Lab.Cli` (komut satiri) → `unity/` (sunum) → `levels/` (metin seviye formati) → `tests/Match3Lab.Core.Tests`.

## Dogrulama — Unity acmadan
Hizli ve gercek: cekirdek degisikligi **her zaman** `dotnet test` ile dogrulanir. Unity'yi ancak sunum/editor isinde ac.

```
dotnet test tests/Match3Lab.Core.Tests
dotnet run --project tools/Match3Lab.Cli -- validate levels
dotnet run --project tools/Match3Lab.Cli -- sim levels/06-cold-storage.txt --runs 2000
dotnet run --project tools/Match3Lab.Cli -- curve levels --runs 1000 --csv curve.csv
```
Unity tarafi gerekiyorsa: `unity test unity --mode PlayMode` (cikis 8 = test basarisiz, 6 = derleme/altyapi).

Simulasyon **rastgeleliktir**: bir iddia icin `--seed` sabitle veya yeterli `--runs` kullan. Tek kosunun sonucunu kanit sayma; zorluk degisimini bant olarak raporla (`tune --band 0.55:0.80`).

## Yazili kurallar
- Seviye formati **insan tarafindan diff'te okunabilir** kalmali. Sikistirma, tek satira toplama, binary'e cevirme yok.
- Mimari karar verdiysen `docs/decisions/` altina kisa bir ADR yaz (neden dahil). Karar orada yasiyor, konusmada degil.
- `docs/media/` ve `Builds/` uretilmis ciktilar — elle duzenlenmez.
- CI: `.github/workflows/tests.yml`. Yesil olmayan bir seyi "tamam" saymayiz; `gh run list -L 3` ile bak.
- Kok dizindeki `unity-*.log` dosyalari gecici tanilama ciktisi; commit edilmez, buyukse `log-avcisi` subagent'ina okut.

## Su an acikta olanlar
`LevelEvolver` yalnizca cekirdekte duruyor; CLI'da ve Level Editor'da karsiligi yok. Tasarimcinin grid'ini yeniden yazdigi icin bir onay adiminin arkasinda durmali (`docs/decisions/0005-layout-evolver.md`). Geri kalan acik kural hatalari README'nin "Known issues" bolumunde: her biri olculen zorlugu degistirdigi icin egri yeniden olculerek kapatilir.
