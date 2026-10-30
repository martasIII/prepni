# Přepni

Napsal jsi `slo6it8`, protože byla zapnutá anglická klávesnice? **Dvakrát klepni na Shift** a z textu bude
`složitá`, klávesnice se přepne na češtinu a můžeš psát dál. Funguje to i obráceně: `+ěšč` → `1234`.

Každé další dvojí klepnutí přibere o slovo víc, takže jde opravit i celá věta, ale jen tak daleko, jak chceš
(„ta 2 bude dost slo6it8“: dvojka zůstane dvojkou). Delší text stačí **označit** a dvakrát klepnout na Shift:
převede se celý. A když se oprava nepovede, vrátí ji Ctrl+Z.

## Stažení

Hotový `Prepni.exe` je u [posledního vydání](../../releases/latest): jeden soubor, nic se neinstaluje, .NET je
uvnitř. Po spuštění se objeví ikonka ⇄ v oznamovací oblasti; v její nabídce jde zapnout spouštění s Windows.

Soubor zatím není digitálně podepsaný, takže Windows při prvním spuštění můžou ukázat varování SmartScreen
(„Další informace“ → „Přesto spustit“). Kdo nechce věřit cizímu exe, může si ho postavit sám, viz níž.

## Jak to funguje

Aplikace si nepamatuje znaky, ale **fyzické klávesy** posledních slov. Při opravě je „zmáčkne“ znovu
v druhém nainstalovaném rozložení (přes stejnou funkci Windows, kterou používá psaní), smaže původní text
a napíše nový. Nepotřebuje slovník ani převodní tabulky: funguje s jakoukoli dvojicí rozložení, české
QWERTZ i QWERTY, programátorské, německé…

| Projekt | Co obsahuje |
|---|---|
| `src/Prepni.Core` | Převod kláves mezi rozloženími, skládání mrtvých kláves (čárka, háček), poslední slova. Bez Windows, pokryté testy. |
| `src/Prepni` | Aplikace v oznamovací oblasti: háček klávesnice, dvojí Shift, přepis přes `SendInput`. |
| `tests/Prepni.Core.Tests` | Unit testy (xUnit). |

## Soukromí

Aplikace technicky sleduje klávesnici, a tak je dobré vědět přesně, co dělá:

- Pamatuje si jen **posledních pár slov, jen v paměti**. Nic neukládá na disk a nic neposílá.
- Paměť se maže při Enteru, kliknutí myší, přepnutí okna, klávesové zkratce nebo šipkách.
- Označený text si půjčí přes schránku (Ctrl+C) a schránku pak vrátí, jak byla.
- Dokud běží, vypne zkratku Windows „pětkrát Shift = Jedno stisknutí“, se kterou se dvojí Shift tluče; při
  ukončení ji vrátí. Kdo Jedno stisknutí používá, tomu ji nechá.
- Celý kód je tady, ať se dá zkontrolovat.

## Spuštění

```bash
dotnet test
dotnet run --project src/Prepni
```

Vydání: tag `v*` (třeba `git tag v0.1.0 && git push --tags`) postaví `Prepni.exe` a přiloží ho k vydání.

## Licence

[MIT](LICENSE)

Ikonka ⇄ se objeví v oznamovací oblasti; pravým tlačítkem jde pozastavit nebo ukončit.

## Co zatím neumí

- Okna spuštěná jako správce (Windows nedovolí psát do nich z běžné aplikace).
- Neví, kde je kurzor: když v textu skočíš jinam bez myši a šipek (třeba Ctrl+Z), oprava může zasáhnout špatné místo.
- Pole pro heslo zatím nepozná.
- Oprava se nijak nezvýrazní; zatím je vidět jen v popisku ikonky.
- Ve Visual Studiu a VS Code kopíruje Ctrl+C bez označení celý řádek; to aplikace pozná a nic nepřepíše.
  Jiné editory s podobným chováním zatím nezná.
