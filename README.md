# Videojuego 2D: el recolector

Proyecto de Unity de la unidad 3 de Optativa Abierta III. Un recolector de basura recorre la calle detrás del camión; cada entrega de la unidad agrega una parte del juego sobre este mismo proyecto.

## Abrir el proyecto

1. Clona el repositorio.
2. En Unity Hub, entra a Projects > Add > Add project from disk y elige la carpeta clonada (la que contiene `Assets`, `Packages` y `ProjectSettings`).
3. Ábrelo con Unity 6000.5.9f1. Usa la plantilla Universal 2D (URP) y el Input System. La primera importación genera `Library` y tarda unos minutos.
4. Abre `Assets/Scenes/Calle.unity`, la escena principal y la primera de Build Settings, y presiona Play.

## Teclas

| Tecla | Estado del personaje |
|---|---|
| Flecha derecha o D | Correr, mirando a la derecha |
| Flecha izquierda o A | Correr, mirando a la izquierda |
| Ninguna, o las dos direcciones a la vez | Reposo, mirando hacia la última dirección |

## La escena y el personaje (entrega 3.1)

- `Grid > Suelo`: Tilemap de la calle, pintado con la paleta `Assets/Arte/Paleta/Calle.prefab` (Window > 2D > Tile Palette).
- `Fondo`: fachadas de la calle, repetidas a lo largo.
- `Main Camera`: ortográfica; encuadra la calle y al personaje.
- `Recolector`: SpriteRenderer, Animator y `RecolectorControl`.

Los sprites del recolector salen de dos tiras (`Assets/Arte/Recolector/`) cortadas en modo Multiple, con el pivote en los pies. El controlador `Assets/Animaciones/Recolector.controller` tiene dos estados: Reposo (por defecto) y Correr, con transiciones en ambos sentidos sin Has Exit Time y el parámetro bool `corriendo`.

`Assets/Scripts/RecolectorControl.cs` lee el teclado con el Input System (`Keyboard.current`), pone `corriendo` en verdadero mientras hay una dirección pulsada y voltea al personaje con la escala en x.

En esta entrega el personaje no se desplaza: cambia de animación y de orientación según la tecla. La física, el salto, la cámara que lo sigue y el movimiento por la calle llegan en las entregas siguientes.

## Qué se versiona

`Assets`, `Packages` y `ProjectSettings`, con sus archivos `.meta`. `Library`, `Temp`, `Logs`, `UserSettings` y las compilaciones quedan fuera por el `.gitignore`.
