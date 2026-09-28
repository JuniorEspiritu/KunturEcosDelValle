# KUNTUR: Ecos del Valle

Aventura 3D de mundo abierto ambientada en **Huancayo, Perú**. Juegas como **Kuntur**, un cóndor andino con chullo y poncho, que recorre su ciudad a pie o en su **tuk tuk**, ayuda a los vecinos y limpia el valle y el **río Mantaro**.

Proyecto del curso **Desarrollo de Videojuegos** (6.º ciclo), Universidad Continental.
Autor: **Junior Saul Ramirez Espiritu**.

ODS: **6** (Agua limpia y saneamiento) y **11** (Ciudades y comunidades sostenibles).

## Tráiler

`Trailer/Kuntur_Trailer.mp4` (30 s).

## Qué tiene

- Huancayo en mundo abierto: Plaza Constitución y la catedral, Jr. Arequipa, Calle Real, Giráldez, Av. Huancavelica, Plaza Vea, heladerías, la bodega andina, el puente sobre el Mantaro, el puente peatonal y el mirador en el cerro.
- Ciudad viva: tráfico que da la vuelta a las manzanas, peatones, semáforos, perros y gatos, ciclo de día y noche.
- Tuk tuk propio: subir, manejar, claxon, sonidos de motor y choque, y "Pedir mi tuk tuk", que llega solo por las calles hasta donde estés.
- Misiones de los vecinos con ruta guiada, recolección de basura, mochila, muestras de agua, barra de Salud del Valle y puntaje.
- Menú principal, tutorial, pausa con volumen de música / ambiente / efectos, minimapa y mapa completo.

## Controles

| Acción | Tecla |
|---|---|
| Moverse | W A S D |
| Correr | Shift |
| Saltar | Espacio |
| Interactuar | E |
| Subir / bajar del tuk tuk | F |
| Manejar / frenar / claxon | W A S D / Espacio / E |
| Mapa completo | M |
| Mochila | Tab |
| Pausa (Pedir mi tuk tuk) | Esc / P |

## Cómo abrirlo

1. Unity **6000.6.0f1** con **URP**.
2. Abrir la carpeta del proyecto desde Unity Hub.
3. Escena de inicio: `Assets/_Project/Scenes/MenuPrincipal.unity`.

La escena del pueblo (`Exploracion.unity`) se arma con los scripts de `Assets/_Project/Editor` (KunturSceneBuilder); se reconstruye sola al abrir el proyecto.

## Estructura

- `Assets/_Project/Scripts`: jugador, vehículos, misiones, UI y sistemas (día/noche, audio, tráfico).
- `Assets/_Project/Editor`: generador de la escena y herramientas de prueba.
- `Assets/_Project/Art`: modelos de Kuntur (Mixamo), audio y música.
- El resto de `Assets/` son paquetes de la Asset Store (ciudad low poly, vehículos, árboles, cielos, animales).
