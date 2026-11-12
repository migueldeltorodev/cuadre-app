# Propuesta de producto

> **Versión:** 0.2 · **Fecha:** 10/11/2026 · **Estado:** aprobada para pasar al diseño visual
> **Nombre de trabajo:** **Cuadre**, por "cuadrar la caja", que es lo que la app hace cada noche. Se puede cambiar.
> **Base:** `REQUISITOS_APP.md` v1.1 (el **qué**). Este documento explica el **para qué** y el **para quién**.

---

## 1. En una frase

> **Una app de escritorio que en 5 minutos, al final del día, te dice cuánto vendiste, si falta plata o mercadería, y cuánto poner en cada sobre.**

---

## 2. El problema

El dueño de un comercio chico (en Cuba, en este caso) lleva el negocio en Excel. El resultado, comprobado en los Excel del bodegón entre enero y septiembre de 2026:

| Síntoma | Consecuencia |
|---|---|
| Totales mal calculados (`#VALUE!`, días salteados, totales en 0) | No se sabe cuánto se vendió de verdad |
| Datos copiados de un mes a otro (impuestos, multas, reportes) | Los números de un mes son en parte de otro |
| Filas duplicadas y un mes entero perdido (marzo) | Historial poco confiable |
| ~80% de lo cobrado es del proveedor, pero no está separado | Se gasta plata de reposición sin darse cuenta |
| El sueldo del dueño se calcula pero no se registra | No se sabe si el negocio le paga al dueño |
| Compras en USD y ventas en CUP, con una tasa que cambia a diario | Se puede vender perdiendo contra el dólar sin notarlo |
| La plata de transferencias queda atrapada en el banco | No se sabe cuánta plata se puede usar de verdad |
| Se conoce la venta, pero no si falta mercadería o plata | Robos, errores o mermas quedan invisibles |

**El problema de fondo:** el Excel deja anotar cualquier cosa, pero no **controla** nada. El dueño trabaja mucho y aun así no sabe si gana plata.

---

## 3. Para quién

### Usuario principal: el dueño-administrador
- Tiene uno o dos comercios chicos y uno o dos dependientes.
- Sabe usar una PC y un Excel básico. **No es contador.**
- Trabaja de noche, en la casa, después del cierre.
- Le importa saber si gana plata, que no le roben y no perder contra el dólar.

### Participante indirecto: el dependiente
- **No usa la app.** Llena la planilla de conteo impresa y entrega la plata al cierre.
- La app registra quién trabajó cada día, para reclamar faltantes.

### Contexto que condiciona el diseño
| Condición | Qué implica |
|---|---|
| Sin internet confiable | Todo funciona sin conexión |
| Cortes de luz | Nada queda a medio guardar, y hay respaldos automáticos |
| Una PC en la casa, nada en el mostrador | Conteo en papel y carga nocturna |
| Dos monedas (CUP y USD) y tasa informal | El dólar está presente en todas las pantallas de costos y precios |
| Plata atrapada en el banco | Cajas separadas de sobres |

---

## 4. Propuesta de valor

| # | Promesa | Cómo se cumple |
|---|---|---|
| 1 | **"Cerrás el día en 5 minutos."** | Cierre guiado paso a paso: cargás el conteo y lo cobrado, la app calcula el resto |
| 2 | **"Sabés si falta algo, todos los días."** | Venta teórica contra cobrado, diferencia de caja y faltantes con seguimiento para reclamar |
| 3 | **"Cada peso tiene destino."** | Reparto automático en sobres (Profit First), diciendo cuánto efectivo poner en cada uno |
| 4 | **"No perdés contra el dólar."** | Costo en USD, precio con colchón, alerta cuando la tasa pasa el colchón |
| 5 | **"Tus números no se rompen."** | Nada se escribe a mano, nada se borra, todo tiene fecha |
| 6 | **"Tus datos sobreviven a la PC."** | Respaldo automático en dos lugares, y CSV y Excel cuando los necesites |

---

## 5. Lo que NO es

- **No es un punto de venta.** No se usa en el mostrador ni venta por venta.
- **No es un sistema contable formal.** No reemplaza a Versat ni la contabilidad oficial; le exporta datos.
- **No es multiusuario ni está en la nube.** Una PC, una persona.
- **No es un ERP.** No hay producción, recetas, facturación electrónica ni nómina formal.

Tenerlo claro protege el alcance. Cada vez que aparezca una idea nueva, la primera pregunta es: ¿esto es para el cierre diario y para saber si gano plata?

---

## 6. Jornadas de uso

### 6.1 Un día normal
| Momento | Dónde | Qué pasa |
|---|---|---|
| Mañana | Casa → mostrador | Se lleva mercadería del almacén. Se anota en la planilla (traslados). |
| Durante el día | Mostrador | El dependiente vende y anota a mano lo que se rompe o se lleva la casa. |
| Cierre | Mostrador | El dependiente cuenta lo que queda en la planilla impresa y entrega la plata. Se cobra su pago. |
| Noche | PC | El dueño hace el **cierre guiado** y la app le dice cuánto poner en cada sobre. Respaldo automático. |

### 6.2 Una compra
1. El dueño cambia CUP por USD (movimiento entre cajas).
2. Compra mercadería en USD y la carga en la app con la tasa del día.
3. La app recalcula el costo promedio y sugiere un precio nuevo; el dueño decide si lo adopta.
4. Si cambió el precio, se imprime la lista de precios para el mostrador.

### 6.3 Fin de mes
1. La app avisa si quedaron días sin cerrar.
2. Reporte mensual: A1/A2/A3, sobres, pérdidas y faltantes.
3. Evaluación Profit First: % reales contra objetivos.
4. Exportación a Excel y CSV si hace falta.

---

## 7. Alcance por versiones

### v1: "El cierre diario confiable" (MVP)
Sin esto la app no sirve:
- Configuración: negocio, formas de cobro, cajas, sobres y %.
- Catálogo de productos y proveedores.
- Compras con costo en USD y precio sugerido.
- Inventario con dos ubicaciones, traslados y salidas no venta.
- **Cierre diario completo**: conteo, cobro, diferencia, pago al dependiente y reparto en sobres.
- Faltantes con seguimiento.
- Gastos recurrentes y puntuales.
- Saldo de sobres por caja.
- Tablero con alertas.
- Respaldo automático y exportación a Excel y CSV.

### v1.1: "Entender el negocio"
- Gráficas de precios y costos por categoría, subcategoría y proveedor.
- Rankings de productos y reporte de pérdidas.
- Evaluación instantánea de Profit First.
- Retiros del dueño e impuestos, contra lo asignado.
- Importar catálogo desde Excel.

### v2: "Crecer"
- Carga automática de la tasa.
- Varios puestos o empleados con esquemas distintos.
- Mapeo a Versat.
- Importar los Excel históricos.
- Más monedas.
- Varios usuarios (dependiente con permisos limitados).

**¿Por qué este orden?** Sin un cierre diario confiable, los reportes de la v1.1 se calculan con datos basura. **Primero datos buenos, después análisis.**

---

## 8. Cómo sabemos si funciona

| Métrica | Objetivo | Cuándo se mide |
|---|---|---|
| Días hábiles cerrados en la app | **100%** | Mensual |
| Tiempo de cierre diario | **< 5 min** | Primeras semanas |
| Excel de inventario abandonado | **Sí**, al terminar el primer mes | Mes 1 |
| Saldo de cada sobre sin hacer cuentas | En **< 10 segundos** desde el tablero | Prueba de uso |
| Totales con error | **0** (son calculados) | Siempre |
| Faltantes con resolución | **100%** recuperados o marcados como perdidos | Mensual |
| Antigüedad del respaldo externo | **< 7 días** | Siempre |
| Sueldo del dueño | **Registrado** cada mes | Mensual |

---

## 9. Principios para el diseño visual

Son el puente al siguiente paso:

1. **El cierre diario es la pantalla estrella.** Es un asistente paso a paso, con el progreso visible y sin poder saltear pasos.
2. **Primero los números.** Montos grandes y legibles, siempre con su moneda (CUP o USD).
3. **Colores con significado, nunca decorativos.** Verde = cuadra, rojo = falta o negativo, ámbar = alerta (colchón, stock, respaldo).
4. **Pensada para cargar con el teclado.** Tab, Enter y foco automático. Cargar 10 conteos no debería requerir el mouse.
5. **Sin jerga contable en la interfaz.** "Lo que entró", "Lo del proveedor", "Lo que es tuyo de verdad", con la sigla A1/A2/A3 como apoyo.
6. **Las alertas se ven al entrar.** El tablero muestra primero lo que requiere acción.
7. **Adaptable:** la referencia es una laptop de 16" (1920×1080). Tiene que funcionar bien desde 1366×768 y aprovechar pantallas más grandes.
8. **Para copiar a mano:** no hay impresora. La planilla de conteo y la lista de precios tienen vistas limpias, de letra grande y sin ruido, pensadas para pasarlas a papel. La impresión queda para el futuro.
9. **Para un solo dueño:** no hay pantallas de bienvenida, ni configuración guiada para terceros, ni marca comercial. Solo hace falta lo necesario.

---

## 10. Supuestos y riesgos

| Riesgo | Probabilidad | Mitigación |
|---|:-:|---|
| **Disciplina:** si no se cierra cada día, la app pierde su valor | Alta | Aviso de días sin cerrar, cierre en menos de 5 minutos, lo menos posible que cargar |
| Olvidar cargar la tasa del día | Media | Aviso al iniciar el cierre; propone la última tasa conocida |
| Conteo mal hecho en el mostrador | Media | La diferencia de caja lo muestra enseguida, y la planilla impresa ordena el conteo |
| El modelo de sobres por caja confunde al usuario | Media | El cierre dice en palabras "poné X en el sobre Y"; el detalle técnico queda en segundo plano |
| Se rompe la PC | Media | Respaldo en dos destinos y aviso de respaldo atrasado |
| El alcance crece (que también haga la cafetería, POS…) | Alta | Sección 5 ("Lo que NO es") y versiones bien definidas |

---

## 11. Preguntas abiertas

| # | Pregunta |
|---|---|
| ~~PP-01~~ | Nombre de trabajo: **Cuadre**. |
| ~~PP-02~~ | **Solo para uso propio** por ahora. La app sigue siendo genérica en sus datos, pero no se invierte en incorporación de terceros ni en licencias. |
| ~~PP-03~~ | Laptop de 16", resolución estándar. Interfaz adaptable. |
| ~~PP-04~~ | **No hay impresora.** Se copia a mano desde pantalla. La impresión queda para el futuro. |
