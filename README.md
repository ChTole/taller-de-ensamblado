# Taller de Ensamblado

Simulación de carpintería para navegador: armar muebles a partir de sus piezas, uniéndolas con encastres de caja y espiga.

Es un **modelo de referencia**, desarrollado sin supervisión de especialistas en carpintería. Las piezas, medidas y procedimientos son ilustrativos y no reemplazan la formación práctica. Es una demo que sienta las bases técnicas para abordar aprendizajes simulados, y registra cada sesión con [Bitácora](https://github.com/ChTole/bitacora) para analizar cómo se resuelve el armado.

## Estado

🚧 En desarrollo.

- [ ] **Versión 1:** tres muebles, solo con encastres.
  - [x] Sistema de ensamblado: manipulación, encastres, deshacer y telemetría.
  - [ ] Escenas:
    - [x] Menú principal
    - [x] Simulación: escritorio y móvil.
    - [ ] Resumen.
  - [x] Muebles:
    - [x] Mesa ratona (5 piezas): modelada e integrada.
    - [x] Taburete (9 piezas): modelado e integrado.
    - [x] Silla (10 piezas): modelada e integrada.
- [ ] **Versión 2:** los mismos muebles, con uniones atornilladas.

## Cómo se juega

1. En el menú principal, elegir la dificultad, es decir, el mueble a armar, y presionar **Comenzar**.
2. Encastrar las piezas sobre la mesa de trabajo. **Ver ayuda**, mientras se mantiene presionado, muestra el mueble armado.
3. Presionar **Comprobar** para terminar y ver el resumen.

| Acción | Control |
|---|---|
| Agarrar y mover una pieza | Clic izquierdo y arrastrar |
| Subir o bajar la pieza agarrada | Shift mientras se arrastra |
| Girar 90° sobre el eje vertical | A / D o flechas izquierda / derecha |
| Volcar 90° hacia adelante o atrás | W / S o flechas arriba / abajo |
| Deshacer el último encastre | Ctrl + Z |
| Sacar la pieza bajo el cursor | X |
| Orbitar la cámara | Clic derecho y arrastrar |
| Acercar o alejar la cámara | Rueda del mouse |

## Requisitos

- Unity 6.3 LTS, con el módulo **Web Build Support**.

## Abrir y compilar

1. Clonar el repositorio y agregarlo desde Unity Hub con **Add → Add project from disk**.
2. Al abrir el proyecto, Unity descarga las dependencias.
3. Abrir la escena `MainMenu` y presionar Play.

Para compilar: **File → Build Profiles → Web**, con las escenas `MainMenu`, `Simulation` y `Feedback` en ese orden en la **Scene List**.

## Dependencias

[Bitácora](https://github.com/ChTole/bitacora) registra las sesiones y los eventos del armado. Está declarada en `Packages/manifest.json` con una versión fija, así que se instala automáticamente al abrir el proyecto. Por defecto usa el backend simulado: funciona sin servidor y muestra en la consola los eventos que enviaría.

## Licencia

[MIT](LICENSE).