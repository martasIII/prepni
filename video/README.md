# Video k Přepni

Úvod a závěr jsou animace v [Remotion](https://www.remotion.dev/) (React → MP4) ve stejných barvách jako
ikonka; uprostřed je skutečný záznam obrazovky.

```bash
npm install
npm run studio   # náhled v prohlížeči, ladění časování
npm run video    # out/prepni.mp4: úvod + záznam + závěr
```

Záznam obrazovky ulož jako `public/recording.mp4` (do gitu nejde). Délka celého videa se podle něj spočítá
sama; bez záznamu je uprostřed 15 s zástupné plochy. Samostatně: `npm run intro`, `npm run outro`.
