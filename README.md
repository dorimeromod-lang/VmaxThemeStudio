# VMAX Theme Studio 0.7

Неофициальный редактор тем для NACODEX.

## Возможности

- текстовые элементы со статичным текстом и датчиками NACODEX;
- автоматическая подстройка цвета текста под фон или обои;
- выбор целевого цвета для автоподстройки;
- полосы и кольца прогресса;
- картинки, шрифты, фон-изображение и видео-фон;
- экспорт темы в `Setting.txt`, `demo.png` и `.vmax`;
- установка темы в NACODEX.

## Требования

- Windows 10/11 x64;
- .NET Framework 4.8;
- Roslyn `csc.exe` из Visual Studio или Build Tools;
- `ffmpeg` — опционально, для видео-фона.

## Сборка

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

После сборки появится `VmaxThemeStudio.exe`.

## Запуск

```powershell
.\VmaxThemeStudio.exe
```

## Проверки

```powershell
.\VmaxThemeStudio.exe --selftest
.\VmaxThemeStudio.exe --selftest-video
.\VmaxThemeStudio.exe --smoke
```

## Статус

Версия 0.7.

## Лицензия

MIT. См. [LICENSE](LICENSE).

## Дисклеймер

Проект не связан с разработчиками NACODEX и не содержит файлов NACODEX. Названия и товарные знаки принадлежат их владельцам.
