# Kuntur: Ecos del Valle — Guía de implementación en Unity

Esta guía te lleva de las 7 pantallas de Figma que ya tienes aprobadas a un proyecto de Unity jugable: personaje que camina/corre/salta, las 3 mecánicas core (basura, muestras de agua, diálogo de concientización), Salud del Valle, puntaje y cuenta regresiva del clímax.

## 0. Por qué te entrego código + guía, y no un proyecto Unity ya armado

Trabajo en un entorno en la nube que no tiene el Editor de Unity instalado — puedo generar y editar archivos de texto/código, pero no puedo abrir Unity, arrastrar objetos a una escena, ni darle Play para probarlo. Por eso el entregable son:

1. **Scripts C# completos**, listos para pegar en tu proyecto (uno por archivo, sin fragmentos sueltos).
2. **Esta guía paso a paso** para armar la escena, el Canvas de cada pantalla y conectar todo, con los valores exactos (colores, textos, tamaños) que ya usaste en tus mockups HTML/Figma — no aproximaciones.
3. **Tests de Edit Mode** para la lógica que más importa (Salud del Valle, puntaje, misiones), que sí puedes correr y ver en verde en tu propia máquina.

Vas a hacer el armado en el Editor en tu laptop siguiendo esta guía de forma prácticamente mecánica: cada sección te dice qué GameObject crear, qué componente agregarle y qué campo del Inspector conectar a qué script.

Perfil técnico asumido (el mismo que ya usamos para el GDD): **Unity 6.6 (6000.6.0f1), 3D, Universal Render Pipeline (URP), nuevo Input System.** Si tu Unity Hub tiene instalada otra versión 6000.x, no pasa nada — todo lo de aquí funciona igual en cualquier Unity 6.

---

## 1. Crear el proyecto

1. Abre **Unity Hub → New Project**.
2. Plantilla: **3D (URP)**. Si Unity Hub te pide descargar el template la primera vez, acéptalo.
3. Nombre del proyecto: `KunturEcosDelValle` (o el que prefieras).
4. Una vez abierto, ve a **Window → Package Manager** y confirma que **Input System** e **Input System Debugger** (o simplemente "Input System") aparezcan instalados — si no, instálalo desde ahí (Unity Registry). Cuando te pregunte si quieres cambiar el backend de Input al nuevo sistema, dile que sí y deja que reinicie el Editor.

## 2. Importar el personaje controlable (Starter Assets)

En vez de programar el movimiento desde cero, usa el paquete oficial y gratuito de Unity:

1. **Window → Asset Store** (o el sitio web de Asset Store si tu versión de Unity ya no tiene la ventana integrada) y busca **"Starter Assets - Third Person Character Controller"** (de Unity Technologies).
2. Agrégalo a tu cuenta y luego impórtalo a este proyecto desde **Window → Package Manager → My Assets**.
3. Al importar, Unity crea una carpeta `Assets/StarterAssets/` con un prefab de personaje ya con Animator, cámara en tercera persona, y caminar/correr/saltar funcionando con el nuevo Input System.
4. Arrastra el prefab `PlayerArmature` (o el que traiga el paquete) a tu escena — ese es tu Kuntur jugable en cuanto a movimiento. Más adelante en el paso 8 le cambiamos el modelo visual si quieres que se vea como el condor de tus imágenes (ver nota al final de esa sección).

Esto resuelve "que el personaje se mueva, corra y salte" sin que tengas que escribir ni depurar un controlador de movimiento propio — es exactamente lo que recomendaba el plan del GDD.

## 3. Assets 3D del pueblo (calles, casas, río)

Tus imágenes de Figma son **arte de referencia**, no assets 3D listos para Unity — sirven para que tú (o quien te ayude a armar el nivel) sepas qué paleta de colores y qué composición de calles/casas/río buscar. Para el bloque 3D real:

- Busca en el **Asset Store** paquetes gratuitos de tipo *"Low Poly Village"*, *"Low Poly Town"* o *"Low Poly Street"* — hay varias opciones gratuitas con casas, calles, farolas y vegetación de baja poligonización que calzan bien con la estética de tus imágenes (colores planos, formas simples). Revisa 2-3 opciones y elige la que tenga casas + calles + algo de vegetación en un solo pack para no combinar demasiados estilos distintos.
- Para el río, un plano alargado con un material azul semi-transparente (o el shader "Water" que trae URP en **Window → Rendering → Lighting → Environment**, sección de agua si tu versión lo incluye) es más que suficiente para un proyecto de curso — no necesitas un shader de agua realista.
- Si no encuentras un pack que te convenza a tiempo, puedes bloquear las calles con **ProBuilder** (Package Manager → ProBuilder, gratuito y oficial de Unity): cubos alargados para calles/veredas y cubos para casas es suficiente para el Conso 1; siempre puedes reemplazar por modelos más bonitos para el Parcial.

No hace falta que el nivel sea grande: con 1-2 calles, un tramo de río con 4 puntos de muestreo, unas 6-8 zonas de basura y las casas de Doña Rosa/Yamile/Kiko ya tienes espacio de sobra para todas las mecánicas.

## 4. Estructura de carpetas

Crea esta estructura dentro de `Assets/_Project/` (el prefijo `_Project` la mantiene arriba en la ventana Project, separada de lo importado):

```
Assets/
  _Project/
    Scenes/
      MenuPrincipal.unity
      SeleccionCiudad.unity
      Huancayo_Exploracion.unity
    Scripts/
      Core/        <- UIPalette, IInteractable, GameManager, ScoreManager, ValleyHealthManager
      Player/      <- InteractionSystem
      Systems/     <- ObjectiveSystem, CountdownManager
      Mechanics/   <- TrashPickup, WaterSampleKit, DialogueNPC
      UI/          <- HUDController, DialogueUI, DialogueOptionUI, ObjectiveRowUI, ScorePopup, ResultScreenController, MenuController
    Tests/
      EditMode/    <- ScoreManagerTests, ValleyHealthManagerTests, ObjectiveSystemTests
    Prefabs/
    Art/
    Input/
```

Copia cada script `.cs` que te entregué en la carpeta que le corresponde (los nombres de carpeta arriba coinciden con los que ya usé al organizarlos en el ZIP/archivos que recibiste).

## 5. Input Actions (tecla E y tecla F)

1. En `Assets/_Project/Input/`, clic derecho → **Create → Input Actions**. Nómbralo `PlayerControls`.
2. Ábrelo (doble clic). Si ya existe un Action Map de Starter Assets, puedes agregar las acciones ahí mismo o crear un Action Map nuevo llamado `Gameplay`.
3. Agrega dos acciones de tipo **Button**:
   - `InteractPrimary` → binding `<Keyboard>/e`
   - `InteractSecondary` → binding `<Keyboard>/f`
4. Guarda el asset (**Save Asset** arriba a la izquierda del editor de Input Actions).
5. En el Inspector de este asset, activa **"Generate C# Class"** si quieres una API tipada (opcional — el script `InteractionSystem` que te entregué usa `InputActionReference`, así que NO necesitas la clase generada: simplemente arrastrarás las acciones directamente).

## 6. Armar el jugador con interacción

1. Selecciona el prefab/instancia del personaje de Starter Assets en la escena.
2. Agrega el componente **Interaction System** (`InteractionSystem.cs`) a la cámara del jugador (el hijo `PlayerFollowCamera` o similar que trae Starter Assets) — así el raycast sale desde donde el jugador está mirando.
3. En el Inspector de `InteractionSystem`:
   - `Interaction Range`: 3
   - `Interactable Layer`: crea una nueva Layer llamada `Interactable` (Inspector → Layer → Add Layer) y selecciónala aquí. Todos los objetos de basura, agua y NPCs deben estar en esta layer.
   - `Player Camera`: arrastra la cámara del jugador.
   - `Interact Primary`: arrastra la acción `InteractPrimary` del asset `PlayerControls`.
   - `Interact Secondary`: arrastra la acción `InteractSecondary`.

## 7. Managers de la escena de exploración

Crea un GameObject vacío llamado `--- MANAGERS ---` (el nombre con guiones es solo para que resalte en la Hierarchy) y cuélgale, como componentes separados o como hijos con un componente cada uno (cualquiera de las dos formas funciona, elige la que te resulte más ordenada):

- `GameManager`
- `ScoreManager`
- `ValleyHealthManager` (ajusta `Starting Health` a 50 para que arranque en un punto medio, como en el mockup)
- `ObjectiveSystem` — en el Inspector, en la lista `Objectives`, agrega 3 elementos:
  1. `id: hablar_con_yamile`, `description: Hablar con Yamile`, `targetCount: 1`
  2. `id: muestras_agua`, `description: Tomar 4 muestras de agua`, `targetCount: 4`
  3. `id: clasificar_residuos`, `description: Clasificar 6 residuos`, `targetCount: 6`
  4. (secundaria) `id: hablar_con_rosa`, `description: Convence a Doña Rosa de no botar basura al río`, `targetCount: 1`, `isSecondary: true`
- `CountdownManager` (solo necesario si en tu escena de exploración también corre el clímax; si lo separas en otra escena, ponlo ahí)

Estos managers **no** necesitan `DontDestroyOnLoad` salvo `GameManager` (que ya lo hace en su propio `Awake`) — el resto puedes recrearlo por escena si cada nivel resetea su progreso.

## 8. Colocar las mecánicas en el mundo

Para cada punto de basura (6, según el objetivo):
1. Crea un GameObject (puede ser un modelo simple de bolsa/tacho, o un cubo temporal), colócalo en la Layer `Interactable`, agrégale un **Collider** (no necesita ser Trigger) y el componente `TrashPickup`.
2. En el Inspector: `Score Value: 10`, `Health Contribution: 3`, `Objective Id: clasificar_residuos`.

Para cada punto de muestra de agua (4, a lo largo del río):
1. GameObject en Layer `Interactable`, Collider, componente `WaterSampleKit`.
2. `Score Value: 25`, `Health Contribution: 5`, `Objective Id: muestras_agua`.

Para Doña Rosa (NPC de diálogo, cerca del río donde bota basura):
1. GameObject/modelo del NPC en Layer `Interactable`, Collider, componente `DialogueNPC`.
2. `Npc Name: Doña Rosa`, `Npc Role: VECINA · BODEGUERA`.
3. `Opening Line`: *"Ay hijito, esto siempre se ha botado aquí... no creo que un poco de basura le haga tanto daño al río."*
4. `Options` (3 elementos, en este orden — el orden importa porque así se ve en el panel):
   1. `text: "Doña Rosa, esa misma agua es la que después toman sus nietos."`, `isCorrect: true`, `npcResponse: "...tienes razón, no lo había pensado así. Voy a guardar la basura para el camión."`
   2. `text: "¡Le va a caer una multa si sigue botando basura ahí!"`, `isCorrect: false`, `npcResponse: "¿Multa? Ya vete de aquí, no me vengas a amenazar."`
   3. `text: "No pasa nada, un poquito no afecta a nadie."`, `isCorrect: false`, `npcResponse: "Eso mismo pienso yo, hijito."`
5. `Objective Id: hablar_con_rosa`.

Repite el patrón de `DialogueNPC` para Yamile (misión principal) y Kiko (misión secundaria de quemar basura) con sus propios textos, adaptando el GDD.

## 9. El Canvas de cada pantalla — valores exactos del mockup

Todas las pantallas comparten esta base: **Canvas** con `Render Mode: Screen Space - Overlay`, **Canvas Scaler** en modo `Scale With Screen Size`, `Reference Resolution: 1280x720` (el mismo tamaño que usamos para las capturas de Figma). Tipografías: **Anton** (títulos/números, la fuente condensada en mayúsculas) y **Nunito** (texto de cuerpo) — descárgalas de Google Fonts e impórtalas como **TMP Font Asset** vía `Window → TextMeshPro → Font Asset Creator`.

Paleta exacta (ya está como constantes en `UIPalette.cs`, pero la repito aquí para cuando configures colores directamente en el Inspector):

| Uso | Hex |
|---|---|
| Fondo de paneles HUD | `#0E1610` al 78% de opacidad |
| Borde sutil de panel | Blanco al 20% |
| Ámbar (botones, salud media) | `#F6B93B` |
| Borde ámbar / sombra ámbar | `#C97F16` / `#A8621A` |
| Dorado (título, puntaje, estrellas) | `#F5C400` |
| Verde (salud alta, correcto, check) | `#7BE3A0` |
| Azul (agua, tecla F) | `#7DD3FC` |
| Crema (texto claro principal) | `#FFF2DF` |
| Crema suave (fondo del globo de diálogo) | `#FDF3E2` |
| Texto secundario | `#CBD5DF` |

### Pantalla 03 — HUD de exploración (la más importante)

Jerarquía dentro del Canvas:

```
Canvas
  Panel_Minimap        (círculo arriba-izquierda, 150x150, anchor top-left)
  Panel_Puntaje        (anchor top-left, junto al minimap)
    Text_Score          (TMP, Anton, 20px, color Dorado, texto "0 PTS")
  Panel_Misiones        (anchor top-right, 320px de ancho)
    Text_TituloMisiones (TMP, Anton, 13px, "MISIONES")
    Content_Objetivos    (Vertical Layout Group, spacing 8) <- objectiveListParent
      [ObjectiveRow prefab se instancia aquí en tiempo de ejecución]
  Panel_SaludDelValle   (anchor bottom-left, 300px de ancho)
    Text_Label           ("SALUD DEL VALLE")
    Text_Porcentaje       ("64%")
    Slider_Fill (o Image Type=Filled) <- healthFillImage, gradiente Ámbar→Verde
  Popup_Anchor          (RectTransform vacío donde se instancian los ScorePopup) <- popupSpawnArea
  Prompt_Primary        (anchor bottom-center, "E" + texto) <- promptPrimary
  Prompt_Secondary      (anchor bottom-right, "F" + texto) <- promptSecondary
```

Crea el **prefab `ObjectiveRow`**: un GameObject con `Horizontal Layout Group` (icono + texto), un `Image` (o `GameObject` simple) como `checkIcon` y un `TMP_Text` como `label`, y el componente `ObjectiveRowUI` encima referenciando ambos. Guárdalo en `Assets/_Project/Prefabs/`.

Crea el **prefab `ScorePopup`**: un `TMP_Text` dentro de un `CanvasGroup`, con el componente `ScorePopup` encima (campo `label` apuntando al TMP_Text).

Finalmente agrega el componente `HUDController` a un GameObject del Canvas (puede ser el Canvas mismo) y conecta cada campo del Inspector con los objetos de arriba (`healthFillImage`, `healthPercentText`, `scoreText`, `scorePopupPrefab`, `popupSpawnArea`, `objectiveListParent`, `objectiveRowPrefab`, `promptPrimary`/`promptPrimaryText`, `promptSecondary`/`promptSecondaryText`, `interactionSystem`).

### Pantalla 04 — Diálogo con NPC

```
Canvas
  Panel_Dialogo (fondo semitransparente #0e1410, se activa/desactiva completo) <- panelRoot
    Card_Retrato (círculo 150x150, gradiente naranja/marrón, borde 4px Ámbar)
      Text_Inicial  (TMP, Anton, 56px, "R")   <- portraitInitialText
    Text_Nombre     ("DOÑA ROSA", Anton, Dorado claro #FFD77A)
    Text_Rol        ("VECINA · BODEGUERA", Nunito)
    Bubble_Speech   (fondo Crema suave, esquinas redondeadas 20/20/20/4 para simular la "colita" del globo)
      Text_Speech    <- speechText
    Content_Opciones (Vertical Layout Group, spacing 12)
      Option_1, Option_2, Option_3   <- optionButtons[0..2], cada una con Button + número + texto (componente DialogueOptionUI)
```

Estados visuales de una opción (cámbialos por script o por dos variantes de color en el Button/Image, según lo que te sea más simple de armar):
- **Neutra** (antes de elegir): fondo `rgba(30,20,20,0.7)`, borde blanco 20%.
- **Correcta/resaltada**: fondo `rgba(20,40,30,0.85)`, borde Verde `#7BE3A0` con un leve glow pulsante (puedes lograrlo con una `Animation`/`Animator` simple que anime el `Outline` o el color del borde).

Agrega `DialogueUI` a `Panel_Dialogo` y conecta todos los campos.

### Pantalla 05 — HUD del clímax

```
Canvas
  Text_CountdownLabel ("LA FERIA DEL PUEBLO LLEGA EN", Anton 15px, #FFD7A3)
  Text_Countdown       ("04:12", Anton 74px, blanco con contorno #7A3A0F) <- se actualiza con CountdownManager.FormattedTime
  Panel_Vecinos (anchor top-right)
    Text_Titulo ("VECINOS CONVENCIDOS")
    Content_Barras (Horizontal Layout Group, 5 elementos iguales - rellenos = Verde, vacíos = blanco 20%)
    Text_Contador ("3 / 5 vecinos")
  Prompt_Climax (mismo estilo que Prompt_Primary de la pantalla 03)
```

No te entregué un `ClimaxHUDController` dedicado porque su lógica es una versión reducida de `HUDController` (solo lee `CountdownManager.OnTick`/`FormattedTime` y `ObjectiveSystem` para el conteo de vecinos) — puedes reutilizar el mismo patrón de suscripción a eventos que ya viste en `HUDController.cs`, o pedírmelo si prefieres que te lo escriba también como archivo aparte.

### Pantallas 01 (Menú), 02 (Elegir ciudad), 06 (Resultado) y 07 (Fin de partida)

Estas son mayormente UI estática con botones — arma el Canvas copiando el layout visual de cada mockup (título, botones en columna a la derecha para el menú; pines sobre el mapa para elegir ciudad; 3 tarjetas + estrellas para resultado; panel de regiones para fin de partida), y conecta los botones así:

- Botón "JUGAR" (01) → `MenuController.PlayGame()`
- Botón "CONTINUAR" (02) → `MenuController.StartHuancayo()`
- Botón "MENÚ PRINCIPAL" (06 y 07) → `MenuController.GoToMainMenu()`
- Botón "JUGAR DE NUEVO" (07) → `MenuController.StartHuancayo()` (o recargar la escena de exploración)

Para la pantalla 06, agrega `ResultScreenController` y conecta las 3 imágenes de estrella, el sprite lleno/vacío, y los 3 `TMP_Text` de las tarjetas.

## 10. Probar la lógica antes de abrir la escena completa

Antes de armar toda la escena, puedes validar que la lógica core funciona:

1. Copia la carpeta `Tests/EditMode/` dentro de `Assets/_Project/Tests/EditMode/`.
2. Abre **Window → General → Test Runner**, pestaña **EditMode**.
3. Unity va a pedirte crear una Assembly Definition la primera vez — acepta, así los tests quedan separados del código del juego.
4. Corre todos los tests. Deberías ver en verde: acumulación de puntaje, que la Salud del Valle nunca pase de 100 ni baje de 0, y que las misiones con contador (ej. "2/4") solo se marcan completas al llegar al número exacto.

Si algo de esto falla, es más rápido de depurar aquí que jugando la escena completa a mano cada vez.

## 11. Checklist de playtesting manual

Antes de dar el Conso 1 por cerrado, juega al menos una partida completa fijándote en esto (no solo en si "no se cae"):

- ¿Se puede terminar la exploración sin haber recogido nunca basura ni tomado muestras? Si sí, esas mecánicas no son realmente necesarias y hay que atarlas más a la condición de avance.
- ¿Elegir la opción "trampa" en el diálogo dos veces seguidas dejaría al jugador sin poder convencer al NPC? Revisa que siempre pueda reintentar (el script ya lo permite, pero verifícalo jugando).
- ¿El jugador entiende, con solo mirar el HUD, qué le falta para subir la Salud del Valle antes de que se le acabe el tiempo del clímax?

## 12. Build para la entrega

1. **File → Build Settings → PC, Mac & Linux Standalone**, plataforma **Windows**, arquitectura x86_64 (a menos que tu laptop sea Mac/Linux).
2. Agrega las 3 escenas en **Scenes In Build**, con `MenuPrincipal` en el índice 0.
3. Compila y **prueba el .exe fuera del Editor** al menos una vez — ahí es donde a veces aparecen assets que solo estaban cargados en la escena abierta del Editor.
4. Si el curso pide el proyecto completo además del ejecutable, comprime la carpeta del proyecto sin `Library/`, `Temp/` ni `obj/` (Unity las regenera solas y pesan mucho sin aportar nada).

---

Cuando tengas la escena de exploración armada con al menos el jugador moviéndose y un par de `TrashPickup` funcionando, avísame — reviso contigo si algo no se siente bien jugado (ej. si el radio de interacción queda muy corto/largo, o si el ritmo de puntos se siente justo) y seguimos con el HUD del clímax o cualquier ajuste que necesites.
