# Requisitos funcionales: App de gestión para pequeño comercio

> **Versión:** 1.14 · **Fecha:** 14/11/2026 · **Producto:** Cuadre · **Estado:** requisitos funcionales consolidados (sin stack ni diseño)
> **Primer caso de uso:** el bodegón (ver `RESUMEN_PROFIT_FIRST_BODEGON.md`).
> Este documento es la **memoria oficial** de los requisitos. Cualquier cambio se anota en el historial (sección 10).

---

## 1. Visión y alcance

**Qué es:** una app de escritorio local (una PC) para gestionar un comercio minorista pequeño: catálogo, compras, inventario, cierre diario de caja, personal, gastos y reparto del dinero según **Profit First**.

**Por qué:** reemplazar los Excel mensuales. Esos archivos permiten cargar cualquier cosa: texto en celdas numéricas, totales mal calculados, filas duplicadas, datos de un mes copiados en otro y movimientos sin fecha. La app **calcula, valida y bloquea**. Nadie escribe un total a mano.

**Principios:**
1. **Genérica:** sirve para cualquier comercio. Nada fijo para un bodegón: categorías, productos, sobres, formas de cobro y días de cierre se configuran.
2. **Una sola fuente de verdad:** la base de datos local. Excel y CSV son **exportaciones**, no fuentes.
3. **Nada se borra con historial:** se anula o se desactiva, y siempre queda rastro.
4. **Todo movimiento tiene fecha propia.** No hay datos que se copian de un mes a otro.
5. **Cada peso tiene destino:** todo lo cobrado se reparte entre Reposición, pago del dependiente y sobres.

### 1.1 Dentro del alcance (v1)
- Un solo usuario (administrador).
- Monedas: **CUP y USD**.
- Venta registrada **al final del día por diferencia de inventario** (no hay PC en el mostrador).
- Profit First con **sobres de efectivo** y porcentajes configurables.

### 1.2 Fuera del alcance (v1)
- Cafetería, alquiler de espacio y otros negocios del dueño.
- Varios usuarios o roles (empleados que cargan datos).
- Punto de venta en el mostrador (venta por venta, en el momento).
- Venta a crédito (fiado).
- Monedas distintas de CUP y USD.
- Carga automática de la tasa de cambio (se evalúa en el futuro).
- Integración directa con Versat (en v1 solo se exporta CSV).

---

## 2. Glosario

| Término | Significado |
|---|---|
| **A1: Ingreso** | Lo **cobrado** en el día (efectivo + transferencias). No es la venta teórica. |
| **A2: Materiales** | Costo de la mercadería vendida, más el costo subcontratado ligado a ella (flete, descarga). |
| **A3: Ingreso real** | A1 − A2. Sobre esto se aplican los % de Profit First. |
| **Sobre** | **Para qué** es la plata: Ganancia, Dueño, Impuestos, Operación, Reposición… |
| **Caja** | **Dónde está** la plata: Efectivo CUP, Efectivo USD, Banco CUP. |
| **Ubicación** | Dónde está la mercadería: Almacén o Mostrador. |
| **Traslado** | Pasar mercadería de una ubicación a otra (por ejemplo, abrir un saco). |
| **Venta por diferencia** | Lo vendido se calcula con el conteo del mostrador al cierre. |
| **Venta teórica** | Unidades vendidas × precio vigente. Es lo que **debería** haber en caja. |
| **Diferencia de caja** | Cobrado − venta teórica. |
| **Merma** | Mercadería perdida que no se vendió: rotura, vencimiento, pérdida a granel. |
| **Tolerancia de merma** | Porcentaje de pérdida natural aceptado por producto (pesaje a granel). |
| **Costo USD promedio ponderado** | Costo por unidad en USD, promediado según cuántas unidades aporta cada compra. |
| **Tasa de compra** | Tasa USD→CUP (referencia elTOQUE) del día de la compra. Por producto se lleva su promedio ponderado. |
| **Colchón** | **Monto fijo en CUP** que se suma a la tasa de compra para cubrir subidas del USD. |
| **Recargo** | % que se aplica sobre el costo para formar el precio. Un recargo de 25% da un margen de 20%. |
| **Margen** | (Precio − costo) / precio. |
| **Deuda interna** | Lo que un sobre le debe a otro cuando las transferencias superan lo asignado a Reposición. |

---

## 3. Reglas de negocio (fórmulas)

### RN-01: Costo promedio ponderado (USD)
Al entrar una compra:
```
Costo USD nuevo = (Stock actual × Costo USD actual + Cantidad comprada × Costo USD compra)
                  / (Stock actual + Cantidad comprada)
```
El promedio usa el **stock existente**, no todo lo comprado en la historia. Si la compra es en CUP, se convierte a USD con la tasa del día de la compra.

### RN-02: Tasa de compra promedio (por producto)
```
Costo CUP histórico por unidad = promedio ponderado de (Costo USD × Tasa de ese día)
Tasa de compra promedio = Costo CUP histórico / Costo USD promedio
```

### RN-03: Precio sugerido
```
Precio sugerido = Costo USD de la tanda en venta × (Tasa de esa compra + Colchón) × (1 + Recargo)
```
- Usa el costo de la **tanda que se está vendiendo** (la más vieja con stock, D-60): es lo que el precio del cartel paga ahora (D-61). La tanda más nueva se muestra al lado, para avisar antes si no va a entrar en el precio.
- El precio es **estable**. El sugerido cambia **cuando cambia la tanda en venta**, y el usuario decide si lo adopta: la app **nunca cambia un precio sola**.
- **No** se recalcula cuando cambia la tasa del día.

### RN-04: Alerta de colchón superado
```
Si Tasa del día > Tasa de la tanda en venta + Colchón  →  alerta de revisión de precio
```
La misma tasa que usó el precio sugerido (D-61).

### RN-05: Venta por diferencia (por producto, en el mostrador)
```
Vendido = Mostrador inicial + Traslados al mostrador − Salidas no venta − Mostrador final
```

### RN-06: Venta teórica y diferencia de caja
```
Venta teórica = Σ (Vendido × Precio vigente)
Diferencia de caja = A1 − Venta teórica
```
Antes de marcar la diferencia, se descuenta la tolerancia de merma de cada producto.

### RN-06b: Merma
- **La merma no se repone desde ningún sobre.** Se asume como pérdida directa y se informa en los reportes (costo de la merma por producto y por período).
- **Merma registrada** (rotura, vencimiento): baja el stock y su costo va al reporte de pérdidas. No mueve sobres ni entra en A2.
- **Merma dentro de la tolerancia** (pesaje a granel, sin registrar): como sale del conteo, se calcula como "vendida" y su costo entra en A2. La diferencia de caja que genera, mientras quede dentro de la tolerancia, **no** cuenta como faltante.

### RN-06c: Faltantes y sobrantes de caja
Si la diferencia de caja, ya descontada la tolerancia, es negativa, se registra un **faltante**:
- El faltante queda en el historial con fecha, monto, el dependiente de turno y una nota. **Estado:** `Pendiente` → `Recuperado` (total o parcial) o `Perdido`.
- Mientras está pendiente, A1 es lo realmente cobrado, así que el faltante achica el A3 de ese día.
- Si se recupera, la plata entra en la caja **el día que se recupera**, como ingreso por recuperación (separado de la venta), y se reparte como A3 de ese día.
- Si la diferencia es positiva (**sobrante**), también se registra, con una nota.

### RN-07: A2 por tandas (FIFO) más el colchón
```
A2 del día = Σ (Vendido de cada tanda × Costo USD × (Tasa de su compra + Colchón))  +  costos subcontratados del día
```
**Decisión (D-60):** lo vendido sale de las **tandas más viejas primero** (FIFO): cada compra es una tanda, con su costo en USD y su tasa, que quedan fijos. A2 suma además el **colchón**: el precio ya lo cobra, y así esa plata va a **Reposición** en lugar de repartirse como ganancia, para aguantar una subida de la tasa. Reemplaza a D-54.

### RN-08: Pago al dependiente
```
Pago del día = Fijo diario + % × A1
```
Esquema actual: **1,000 CUP + 1% de A1** (ventas reales cobradas, no la venta teórica). El esquema tiene fecha de inicio. El costo es del **puesto**, no depende de qué persona trabajó. Se paga **en efectivo, al cierre**.

### RN-09: Reparto Profit First del cierre
```
A3 = A1 − A2
A2 → sobre REPOSICIÓN
A3 → se reparte en los sobres según los % vigentes (la suma da 100%)
Sobre OPERACIÓN: + asignación − pago al dependiente  (se registran los dos movimientos)
```
- Un sobre puede quedar **en negativo**, con una alerta visible. Nunca se esconde.

### RN-10: Reparto por caja (efectivo y transferencia)
1. Todas las **transferencias** van primero al sobre **Reposición**.
2. El resto de Reposición se completa con efectivo.
3. Los demás sobres y el pago al dependiente se cubren **con efectivo**.
4. **Excepción:** si las transferencias superan A2, el excedente queda en Reposición y se registra una **deuda interna** de Reposición hacia los demás sobres. Se compensa sola en los cierres siguientes con el efectivo que le toca a Reposición.

Ejemplo: A1 = 50,000 (efectivo 40,000 · transferencia 10,000), A2 = 20,000:
```
Reposición  20,000  = 10,000 transferencia + 10,000 efectivo
A3          30,000  = 30,000 efectivo → sobres (de Operación sale el dependiente)
```

### RN-11: Consumo de la casa
Lo que se saca del negocio para la casa es un **retiro del dueño en especie**, valuado **como A2**: por tandas más el colchón (D-60):
- Descuenta stock.
- Se resta del sobre **Dueño** y se acredita al sobre **Reposición**, porque esa mercadería hay que reponerla.

### RN-12: Cambio de moneda
Cambiar CUP→USD es un **movimiento entre cajas** con la tasa conseguida de verdad. **No es gasto.** El sobre no cambia, solo la caja.

### RN-13: Conversión de unidades
Cada producto tiene **unidad de compra** (saco, caja), **unidad de stock y venta** (libra, unidad) y un **factor de conversión** (1 saco = 100 lb). Los productos a granel aceptan cantidades decimales.

---

## 4. Requisitos funcionales

Formato: `RF-<MÓDULO>-<nº>`. Prioridad: **M** = obligatorio en v1 · **D** = deseable en v1 · **F** = futuro.

### 4.1 Configuración general (CFG)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-CFG-01 | Datos del negocio: nombre, dirección, logo (opcional). | M |
| RF-CFG-02 | Días de cierre configurables (por ejemplo, el domingo). Esos días no piden cierre. | M |
| RF-CFG-03 | Formas de cobro configurables, cada una asociada a una caja. Por defecto: Efectivo, Transfermóvil y Enzona. | M |
| RF-CFG-04 | Un solo usuario administrador, con contraseña. | M |
| RF-CFG-05 | Umbral de diferencia de caja a partir del cual se pide una nota. | M |
| RF-CFG-06 | Colchón cambiario general (monto fijo en CUP) y recargo general por defecto. | M |
| RF-CFG-07 | Regla de redondeo de precios (por ejemplo, al múltiplo de 5 o de 10 CUP). | D |

### 4.2 Moneda y tasa de cambio (MON)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-MON-01 | Monedas: CUP y USD. Cada operación lleva su moneda. | M |
| RF-MON-02 | Historial de la tasa USD→CUP (referencia elTOQUE), cargada a mano, una por día. | M |
| RF-MON-03 | Aviso si no hay tasa cargada para el día. | M |
| RF-MON-04 | Los reportes consolidan en CUP. | M |
| RF-MON-05 | Carga automática de la tasa (hay que verificar cómo acceder a ella). | F |
| RF-MON-06 | Otras monedas configurables. | F |

### 4.3 Catálogo de productos (PRD)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-PRD-01 | Un producto es una **variedad o marca** distinta (arroz guyanés ≠ arroz brasileño). El mismo producto comprado a varios proveedores **es un solo producto**. | M |
| RF-PRD-02 | Atributos: código interno, código de barras (opcional), nombre, marca, presentación, categoría y subcategoría, proveedor habitual, unidad de compra, unidad de stock y venta, factor de conversión, permite decimales, precio de venta, stock mínimo, tolerancia de merma (%), colchón propio (opcional), recargo propio (opcional), perecedero (con su vida útil opcional, RF-PRD-10), activo y notas. | M |
| RF-PRD-03 | Valores calculados, no editables: costo USD promedio, tasa de compra promedio, costo CUP histórico, último costo puesto y su tasa, precio sugerido, margen (contra el último costo) y stock por ubicación. | M |
| RF-PRD-04 | Categorías de dos niveles (categoría → subcategoría), creadas por el usuario. | M |
| RF-PRD-05 | Historial de precios de venta y de costos, con fecha. | M |
| RF-PRD-06 | Mostrar recargo y margen real juntos. | M |
| RF-PRD-07 | Un producto con historial no se borra, se desactiva. | M |
| RF-PRD-08 | Importar el catálogo desde Excel. | D |
| RF-PRD-09 | **Lista de precios** en pantalla, agrupada por categoría, con formato para copiarla a mano al cartel del mostrador. Marca los precios que cambiaron desde la última vez que se copió. | M |
| RF-PRD-10 | Un producto perecedero puede guardar su **vida útil** típica (cuánto tarda en vencerse), cargada en días, semanas o meses y guardada en días. Es un dato informativo; el vencimiento estimado por compra queda para la v2 (`REQUISITOS_V2.md`). | M |
| RF-PRD-11 | Desde el formulario del producto se puede **crear una categoría o subcategoría** sin salir de él; queda elegida y no se pierde lo cargado. Lo mismo con el **proveedor** desde la compra. | M |

### 4.4 Proveedores (PRV)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-PRV-01 | Alta de proveedores: nombre, contacto, teléfono y notas. | M |
| RF-PRV-02 | Ficha del proveedor: historial de compras, productos que provee y último costo de cada uno. | M |

### 4.5 Compras (CMP)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-CMP-01 | Registrar una compra: fecha, proveedor, moneda, tasa del día, ubicación de destino (por defecto Almacén), líneas (producto, cantidad en unidad de compra, costo unitario). | M |
| RF-CMP-02 | Costos asociados (flete, descarga). Se reparten en el costo de los productos de esa compra y cuentan como A2. | M |
| RF-CMP-03 | Al confirmar: suma stock (convirtiendo unidades), recalcula el costo promedio (RN-01, RN-02) y el precio sugerido (RN-03). | M |
| RF-CMP-04 | Si cambia el precio sugerido, avisar y dejar que el usuario adopte el precio nuevo o mantenga el actual. | M |
| RF-CMP-05 | Indicar de qué caja y sobre sale el pago (normalmente Reposición, en USD). | M |
| RF-CMP-06 | Historial de compras con filtros: fecha, proveedor, producto, categoría y subcategoría. | M |
| RF-CMP-07 | Una compra confirmada no se edita: se **anula** con un motivo y se vuelve a cargar. | M |

### 4.6 Inventario (INV)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-INV-01 | Ubicaciones configurables. Por defecto: Almacén y Mostrador. | M |
| RF-INV-02 | El stock se calcula a partir de los movimientos. Nunca se escribe a mano. | M |
| RF-INV-03 | Traslados entre ubicaciones, con conversión de unidad (1 saco del almacén → 100 lb en el mostrador). | M |
| RF-INV-04 | Salidas que no son venta, con motivo: merma, vencimiento o rotura, consumo de la casa (RN-11) y ajuste. | M |
| RF-INV-05 | Alerta de stock bajo (total por debajo del mínimo). | M |
| RF-INV-06 | Lista de compras sugerida a partir de los productos con stock bajo. | D |
| RF-INV-07 | Kardex por producto: todos los movimientos con su saldo, por ubicación. | M |
| RF-INV-08 | Conteo físico del almacén (o general) con ajuste de diferencias y motivo. | M |

### 4.7 Cierre diario (CIE)
**Flujo del cierre** (en este orden):
1. Confirmar la tasa del día.
2. Registrar los traslados del día al mostrador (sacos abiertos, cajas repuestas).
3. Registrar las salidas que no son venta (merma, consumo de la casa).
4. Cargar el conteo final del mostrador.
5. Cargar lo cobrado por cada forma de cobro.
6. Revisar el resumen: vendido, venta teórica, A1, A2, A3, diferencia de caja, pago al dependiente y reparto en sobres.
7. Confirmar y cerrar.

| ID | Requisito | Prior. |
|---|---|:-:|
| RF-CIE-01 | **Planilla de conteo** del día en pantalla (productos del mostrador y stock inicial), con formato limpio para **copiarla a mano**. No hay impresora en v1. | M |
| RF-CIE-01b | Imprimir la planilla de conteo y la lista de precios. | F |
| RF-CIE-02 | Cargar el conteo final del mostrador; la app calcula lo vendido (RN-05). | M |
| RF-CIE-03 | Calcular la venta teórica y la diferencia de caja, descontando la tolerancia de merma (RN-06). | M |
| RF-CIE-04 | Nota obligatoria si la diferencia de caja supera el umbral. | M |
| RF-CIE-05 | Calcular A1, A2 (RN-07) y A3. | M |
| RF-CIE-06 | Calcular el pago al dependiente (RN-08) y registrarlo como pagado en efectivo desde el sobre Operación. | M |
| RF-CIE-07 | Calcular el reparto en sobres por caja (RN-09, RN-10) y mostrar **cuánto poner físicamente en cada sobre**. | M |
| RF-CIE-08 | Una sola caja por fecha. Los días de cierre configurados no la piden. | M |
| RF-CIE-09 | Aviso de días hábiles sin cerrar. | M |
| RF-CIE-10 | El día cerrado queda bloqueado. Reabrirlo es explícito, pide un motivo y queda registrado. | M |
| RF-CIE-11 | Detectar posibles duplicados: avisar si los totales del día son idénticos a los de otro día. | D |
| RF-CIE-12 | Registrar faltantes y sobrantes de caja (RN-06c), con el dependiente de turno y una nota. | M |
| RF-CIE-13 | Lista de faltantes pendientes, para reclamarlos; marcar cada uno como recuperado (total o parcial) o perdido. | M |
| RF-CIE-14 | Registrar quién trabajó el día (solo para el historial y los reclamos; no cambia el pago). | M |

### 4.8 Personal (PER)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-PER-01 | Esquema de pago del puesto: fijo diario + % de A1, con fecha de inicio. | M |
| RF-PER-02 | Cambiar el esquema no recalcula los días anteriores. | M |
| RF-PER-03 | Resumen mensual del costo de personal (es gasto de operación, no A2). | M |
| RF-PER-04 | Varios puestos o empleados con esquemas distintos. | F |

### 4.9 Gastos operativos (GAS)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-GAS-01 | Gastos recurrentes (por ejemplo, el gestor): monto, frecuencia y sobre. La app los propone cada período y el usuario los confirma. | M |
| RF-GAS-02 | Gastos puntuales: fecha, monto, moneda, categoría, caja y sobre del que sale. | M |
| RF-GAS-03 | Categorías de gasto configurables. | M |
| RF-GAS-04 | Un gasto registrado no se edita: se anula con un motivo. | M |

### 4.10 Cajas (CAJ)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-CAJ-01 | Cajas configurables. Por defecto: Efectivo CUP, Efectivo USD y Banco CUP. | M |
| RF-CAJ-02 | Cada cobro entra a la caja que corresponde a su forma de cobro. | M |
| RF-CAJ-03 | Cambio de moneda entre cajas, con la tasa conseguida (RN-12). | M |
| RF-CAJ-04 | Saldo por caja, y arqueo: comparar el saldo con el conteo físico. | M |

### 4.11 Profit First: sobres (PF)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-PF-01 | Sección de configuración Profit First con los sobres por defecto **Ganancia, Compensación del Dueño, Impuestos y Gastos de Operación**, con % editables. | M |
| RF-PF-02 | Crear, renombrar y desactivar sobres. | M |
| RF-PF-03 | Validación: los % sobre A3 suman 100%. | M |
| RF-PF-04 | Sobre especial **Reposición de mercadería**: recibe A2, no lleva %. | M |
| RF-PF-05 | Los % tienen fecha de inicio. Cambiarlos no altera los repartos anteriores. | M |
| RF-PF-06 | Saldo de cada sobre **por caja** (efectivo CUP / banco / USD), con historial diario. | M |
| RF-PF-07 | Movimientos de sobre: asignación, pago, retiro y movimiento entre sobres, cada uno con su motivo. | M |
| RF-PF-08 | Saldo negativo permitido, con alerta. | M |
| RF-PF-09 | Deuda interna por exceso de transferencias, con compensación automática (RN-10). | M |
| RF-PF-10 | Evaluación instantánea por período: A1, A2, A3 y % reales frente a los objetivo. | M |
| RF-PF-11 | Reporte de **plata atrapada**: saldo en banco por sobre. | M |

### 4.12 Dueño e impuestos (DUE / IMP)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-DUE-01 | Retiros del dueño en efectivo, desde el sobre Compensación del Dueño. | M |
| RF-DUE-02 | Consumo de la casa como retiro en especie (RN-11). | M |
| RF-DUE-03 | Comparar lo asignado al dueño con lo retirado, por período. | M |
| RF-IMP-01 | Registrar impuestos pagados: fecha, concepto, monto, comprobante (número o adjunto), desde el sobre Impuestos. | M |
| RF-IMP-02 | Comparar lo provisionado con lo pagado, por período. | M |
| RF-IMP-03 | Impuestos configurables (nombre, tasa, sobre ventas o sobre ganancia, activo): solo **sugieren** cuánto apartar, en el resumen del contador y junto al sobre Impuestos (D-62). | M |

### 4.13 Reportes y análisis (REP)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-REP-01 | Tablero: ventas del día y del mes, diferencia de caja, saldo de cada sobre, alertas (stock bajo, colchón superado, días sin cerrar, sobres en negativo, respaldo atrasado). | M |
| RF-REP-02 | Gráfica de evolución de costos y precios por producto, subcategoría o categoría, con filtro por proveedor. | M |
| RF-REP-03 | Comparar el costo del mismo producto entre proveedores. | M |
| RF-REP-04 | Ranking de productos: más vendidos, más margen, más merma. | M |
| RF-REP-08 | Reporte de pérdidas: merma registrada (a costo) y faltantes perdidos, por período. | M |
| RF-REP-05 | Reporte mensual de cierre: A1, A2, A3, personal, gastos, reparto en sobres y diferencias de caja. | M |
| RF-REP-06 | Evolución de la tasa de cambio frente al colchón de cada producto. | D |
| RF-REP-07 | Crecimiento mes contra mes. | D |
| RF-REP-09 | Resumen para el contador: estado de rendimiento del mes y sus totales (D-63). | M |

### 4.14 Datos, respaldo y exportación (DAT)
| ID | Requisito | Prior. |
|---|---|:-:|
| RF-DAT-01 | Respaldo automático al cerrar el día y al cerrar la app. | M |
| RF-DAT-02 | Dos destinos: disco local y un destino configurable (pendrive, otro disco, carpeta sincronizada). | M |
| RF-DAT-03 | Rotación: 7 diarios, 4 semanales y 12 mensuales. | M |
| RF-DAT-04 | **Exportación CSV auditable**: un archivo por entidad, UTF-8, separador configurable, columnas estables, con fecha de generación y hash del archivo. Pensado para auditar y para pasar a otros programas (por ejemplo, Versat). | M |
| RF-DAT-05 | Restaurar un respaldo, guardando antes una copia del estado actual. | M |
| RF-DAT-06 | Aviso si el respaldo externo está atrasado más de N días. | M |
| RF-DAT-07 | Exportar a Excel cada módulo por rango de fechas, y también el reporte mensual. | M |
| RF-DAT-08 | Registro de auditoría: anulaciones, reaperturas de días, cambios de precio y de configuración. | M |
| RF-DAT-09 | Mapeo específico al formato de importación de Versat. | F |
| RF-DAT-10 | Importar los Excel históricos de 2026. | F |

---

## 5. Requisitos no funcionales

| ID | Requisito |
|---|---|
| RNF-01 | App de escritorio para PC (Windows), en español. |
| RNF-01b | Interfaz **adaptable** a cualquier tamaño de pantalla: referencia de 16" (1920×1080), usable desde 1366×768 y con aprovechamiento de pantallas más grandes. |
| RNF-02 | Funciona **sin internet**. Ninguna función esencial depende de la red. |
| RNF-03 | Datos locales en una base de un solo archivo (SQLite, decisión preliminar). |
| RNF-04 | Montos con precisión decimal exacta: nunca con coma flotante. |
| RNF-05 | Soporta cortes de luz: una operación se confirma completa o no se confirma. |
| RNF-06 | El cierre diario completo (unos 10 productos) se hace en menos de 5 minutos. |
| RNF-07 | Pensada para un usuario no técnico: validaciones claras y sin jerga contable en la interfaz. |

---

## 6. Decisiones tomadas

| # | Decisión | Por qué |
|---|---|---|
| D-01 | A2 = solo mercadería y subcontratados. El dependiente es gasto de operación. | Así lo define Profit First. |
| D-02 | A1 = lo cobrado, no la venta teórica. | Se reparte plata que existe. |
| D-03 | A3 = A1 − A2. El dependiente **no** se resta antes, se paga desde el sobre Operación. | Evita contarlo dos veces. |
| D-04 | Venta por diferencia de inventario al cierre, contando solo el mostrador. | No hay PC en el mostrador y el almacén está en la casa. |
| D-05 | Producto = variedad o marca. El proveedor va en la compra; el producto tiene un proveedor habitual. | Los sacos de distintos proveedores se mezclan y se venden como uno solo. |
| D-06 | Costo en USD por promedio ponderado. | Se compra mayormente en USD y la mercadería se mezcla. |
| D-07 | Precio estable: costo USD × (tasa de compra + colchón) × (1 + recargo). Se recalcula solo al comprar. | El precio no puede cambiar todos los días con la tasa. |
| D-08 | Colchón = monto fijo en CUP sobre la tasa. | Más simple e intuitivo. |
| D-09 | A2 a costo **histórico**, no de reposición. | La tasa sube y baja a diario; el colchón cubre las variaciones. |
| D-10 | Separar **cajas** (dónde) de **sobres** (para qué). | La plata del banco no se puede usar como el efectivo. |
| D-11 | Las transferencias van al sobre Reposición; el efectivo se prioriza para los demás sobres. Si sobra transferencia, queda una deuda interna. | La plata del banco queda atrapada; Reposición es el sobre que acumula. |
| D-12 | Pago del puesto: 1,000 CUP/día + 1% de A1, en efectivo al cierre. | Regla vigente del negocio. |
| D-13 | Consumo de la casa = retiro del dueño valuado a costo; se repone a Reposición. | El resumen ya lo clasificaba como sueldo del dueño. |
| D-14 | Un solo usuario administrador en v1. | Simplicidad. |
| D-15 | Base local SQLite + respaldo rotativo + CSV auditable. | App local, de un usuario, con cortes de luz. |
| D-16 | Los datos viejos ("SALARIO DEP", empleados con nombre, gastos puntuales del Excel) no se migran. | Son datos basura o desactualizados. |
| D-17 | La merma no se repone desde ningún sobre: es pérdida directa y se informa. | Simplicidad; la tolerancia ya la contempla. |
| D-18 | Los faltantes de caja se registran como pendientes de reclamo y se siguen hasta resolverlos. | Normalmente se resuelven con el dependiente; si no, tiene que quedar historial para reclamar. |
| D-19 | El % del dependiente se calcula sobre A1 (lo cobrado). | Regla del negocio: "sobre lo real". |
| D-20 | % por defecto: Ganancia 5% · Dueño 50% · Impuestos 15% · Operación 30%. Rigen "desde el principio" hasta que se cargue un reparto nuevo. | Tabla del libro para un ingreso real chico. |
| D-21 | Cada movimiento de caja pertenece a un sobre: en cada caja, la suma de sus sobres es el saldo de la caja. | Así los sobres nunca mienten sobre la plata real. |
| D-22 | Saldo inicial por caja y sobre: se escribe lo que hay físicamente y la app mueve la diferencia con lo que ya tenía registrado (por ejemplo, las compras anteriores). | Arrancar con la plata real sin borrar el historial. |
| D-23 | Las compras y sus costos asociados se pagan siempre desde Reposición. | Reposición recibe A2 justamente para reponer mercadería. |
| D-24 | El consumo de la casa mueve su costo de Dueño a Reposición dentro de la caja de efectivo CUP; no sale efectivo. | RN-11 sin inventar un movimiento de plata que no existió. |
| D-25 | Un reparto nuevo no puede empezar antes del último; si empieza el mismo día, lo reemplaza. | Los % con los que se repartió un día pasado no cambian (RF-PF-05). |
| D-26 | Un día cerrado queda bloqueado: no se puede registrar nada con fecha igual o anterior al último día cerrado (compras, traslados, salidas, conteos, operaciones de sobres, anulaciones, repartos y esquemas de pago nuevos). Las anulaciones se fechan hoy, así que hoy tiene que estar abierto. | Un cierre ya hecho nunca deja de cuadrar con su mostrador ni con su reparto (RF-CIE-10). |
| D-27 | Reabrir un cierre = anularlo con un motivo. Solo se puede reabrir el último día cerrado; sus movimientos se revierten con la fecha del mismo día, así el día queda exactamente como antes de cerrarlo y se puede volver a cerrar. | Corregir sin romper la cadena de mostrador y de deuda de los días siguientes. |
| D-28 | El cierre solo cobra en CUP: pide las formas de cobro activas cuya caja está activa y es en CUP. | Las ventas del mostrador son en CUP; el USD entra por otros caminos. |
| D-29 | Los días se cierran en orden: un cierre nuevo tiene que ser posterior al último y no puede ser futuro. Si se saltea un día, lo vendido entra en el cierre siguiente (el conteo lo absorbe). | La venta por diferencia (RN-05) necesita el conteo anterior. |
| D-30 | Planilla del cierre: los productos con stock en un mostrador antes del día o con movimientos en un mostrador ese día. Vendido = esperado − contado (puede ser negativo: baja la venta y el costo, sin merma). Precio = el vigente ese día; costo = por tandas, FIFO (D-60). Se exige contar todos; un producto vendido sin precio no deja cerrar. | RN-05, RN-06, RN-07 sin huecos. |
| D-31 | Mecánica del cobro: todo lo cobrado entra a Reposición en su caja; después el reparto mueve efectivo de Reposición a cada sobre (primero de la caja de efectivo CUP principal) y el pago al dependiente sale en efectivo del sobre Operación. | Cada peso queda en un sobre (D-21) y el movimiento explica el reparto. |
| D-32 | Deuda interna (RN-10, RF-PF-09): un solo saldo total. Lo que entra por transferencia por encima de A2 queda en Reposición (en el banco) y es deuda con los demás sobres; se devuelve con el efectivo que Reposición recibiría en los días siguientes con A3 positivo, repartido por los % vigentes. | Simple de seguir y nunca deja a los sobres sin su parte. |
| D-33 | Faltante o sobrante = diferencia de caja después de la merma aceptada. Un faltante queda pendiente de reclamar; un sobrante solo se registra. Si supera el umbral, el cierre exige una nota. | RN-06c, RF-CIE-04. La resolución (recuperado / perdido): D-35 y D-36. |
| D-34 | Pago al dependiente = fijo + % de A1, redondeado a pesos enteros. El esquema rige desde una fecha; uno nuevo no puede empezar antes del último y el mismo día lo reemplaza. | Se paga en mano; los días cerrados conservan lo que pagaron (RF-PER-02). |
| D-35 | Recuperar un faltante (total o en partes): el dinero entra el día en que se recupera (hoy por defecto, nunca futuro ni anterior al faltante, y ese día tiene que estar abierto), en una caja CUP activa, y se reparte como un A3 de ese día: por los % vigentes entre los sobres con %; Reposición no recibe nada. Una recuperación no se edita: se anula con motivo y sus movimientos se revierten con fecha de hoy. Cuando no queda nada por recuperar, el faltante pasa a Recuperado; si se anula una recuperación, vuelve a Pendiente. | RN-06c, RF-CIE-13. El faltante ya bajó el A1 de su día: lo recuperado es ingreso real nuevo, y el día cerrado no se toca (D-26). |
| D-36 | Dar por perdido un faltante exige una nota; no mueve dinero (el faltante ya bajó el A1 de su día). Un faltante perdido o recuperado entero ya no admite recuperaciones. Un cierre cuyo faltante tiene recuperaciones confirmadas no se puede reabrir: primero se anulan. | Ninguna recuperación queda colgando de un cierre anulado. |
| D-37 | Días sin cerrar (RF-CIE-09): los días hábiles ya terminados (hasta ayer) posteriores al último cierre; si todavía no hay cierres, desde el primer día con movimientos. Los días que el negocio no abre no cuentan. Los días anteriores al último cierre quedan bloqueados (D-26) y no se avisan. | Un aviso útil, sin ruido de días que ya no se pueden cerrar. |
| D-38 | Posible duplicado (RF-CIE-11): otro día cerrado con exactamente el mismo efectivo, las mismas transferencias y la misma venta teórica. Es solo un aviso en la vista previa y al cerrar; no se compara cuando el día no tuvo ventas ni cobros. | Detecta el error típico de cargar dos veces el mismo día. |
| D-39 | Evaluación por período (RF-PF-10): suma de los cierres confirmados entre dos fechas (por defecto, del 1 del mes a hoy). A2/A1 y A3/A1. Por sobre: lo recibido (reparto de los cierres + recuperaciones que entraron en el período), el % objetivo (el plan vigente al final del período) y el % real sobre lo que recibieron juntos los sobres con %. Incluye la deuda interna al final del período. | Compara lo que se quiso repartir con lo que de verdad se repartió. |
| D-40 | Un gasto sale de una caja activa y de un sobre: Gastos de operación por defecto; puede salir de otro sobre activo, nunca de Reposición (solo paga mercadería). La moneda es la de la caja. Fecha nunca futura y en un día abierto (D-26). Si deja el sobre en negativo se permite con aviso (RF-PF-08). Se numera aparte (Gasto #N). | D-01: A2 es solo el costo de lo vendido; el resto de los gastos sale de lo repartido. |
| D-41 | Un gasto no se edita: se anula con motivo y su movimiento se revierte con fecha de hoy (hoy tiene que estar abierto). El gasto anulado se sigue viendo en el historial pero no suma en los totales. | RF-GAS-04, igual que compras y operaciones de sobres. |
| D-42 | Categorías de gasto: vienen de fábrica (Gestor y contador, Transporte, Servicios, Alquiler, Mantenimiento y reparaciones, Envases y materiales, Otros); se renombran, se agregan y se desactivan, nunca se borran. No se puede desactivar una categoría que usa un gasto fijo activo. | RF-GAS-03. Los gastos guardados conservan su categoría. |
| D-43 | Gasto fijo (recurrente): nombre, categoría, monto, caja, sobre, frecuencia mensual (día 1 a 28, así todos los meses lo tienen) o semanal (un día de la semana) y desde cuándo. La app propone cada vencimiento ya llegado (hasta hoy, como mucho los últimos 12) que no se pagó ni se saltó. Período = el mes, o la semana de lunes a domingo. | RF-GAS-01. Nada se paga solo: el dueño confirma cada vez. |
| D-44 | Confirmar un vencimiento crea un gasto con los datos del gasto fijo; se pueden cambiar el monto, la caja y la fecha (hoy por defecto: el día en que de verdad salió el dinero). Un vencimiento se paga una sola vez; si se anula ese gasto, se vuelve a proponer. Saltarlo («este mes no se pagó») no mueve dinero. Editar un gasto fijo no cambia los gastos ya confirmados. | Sin duplicados y sin perder un pago corregido. |
| D-45 | El cobro que se anota en el cierre es lo que se cobró por las ventas. Si se pagó un gasto con dinero del cajón, se registra como gasto aparte (sale de la caja y de Operación) y en el cierre se anota el cobro completo. | Si no, el gasto aparecería como faltante del dependiente. |
| D-46 | El tablero compara el mes en curso (del 1 a hoy) con **los mismos días** del mes anterior, y muestra el último cierre, los cierres de los últimos 30 días, el stock bajo y los productos con el colchón superado (los primeros 5 y el total). Los saldos de los sobres, los días sin cerrar, los faltantes y los gastos fijos vencidos salen de sus propias pantallas. El aviso de respaldo atrasado llega con el respaldo. | Un mes que recién empieza no tiene que parecer una caída; no repetir datos que ya tienen su fuente. |
| D-47 | **Pérdida de mercadería** = salidas por merma, vencimiento y rotura, más las diferencias negativas de los conteos, valuadas a lo que costaron: por tandas, sin colchón (D-60; antes, al costo promedio de ese momento). El consumo de la casa (retiro del dueño, RN-11) y el ajuste (corrección) no son pérdida. Las diferencias positivas de los conteos se informan aparte y no compensan las pérdidas. | RN-06b sin mezclar retiros ni correcciones con lo que realmente se perdió. |
| D-48 | **Faltante perdido** = el monto del faltante menos lo que se recuperó antes de darlo por perdido. Cuenta en el período del día del faltante. | Es lo que de verdad no volvió, en el día en que faltó. |
| D-49 | El ranking de productos usa la **venta teórica** de los cierres (vendido × precio) y su costo histórico: la plata se cuenta por día, no por producto. Incluye los productos que en el período solo tuvieron pérdidas. | Es la única venta por producto que existe; así también aparecen los que más merma tienen. |
| D-50 | Evolución de costos: **por producto**, cada compra con su costo puesto por unidad de stock (USD y CUP) y cada cambio del precio de venta; **por categoría o proveedor**, el primer y el último costo del período de cada producto y su variación. No se promedian productos distintos. La comparación entre proveedores usa el promedio ponderado por cantidad y nunca se filtra por proveedor. | Un promedio de libras con litros no significa nada. La variación en USD es la del proveedor; la de CUP suma la tasa. |
| D-51 | El reporte mensual va del 1 al último día del mes (el mes en curso, hasta hoy) y reúne la evaluación Profit First, los gastos por categoría y moneda, las diferencias de caja, la merma a costo, cada día y los últimos 12 meses para el crecimiento mes contra mes (RF-REP-07). La evaluación Profit First suma también los gastos del período, por moneda. | Un solo lugar para cerrar el mes; las monedas nunca se suman entre sí. |
| D-52 | *(Reemplazada por D-61.)* El precio sugerido (RN-03), la alerta del colchón (RN-04) y el margen que se ve al poner el precio usan el **último costo puesto**: el de la última compra confirmada del producto, con su tasa. Sin compras confirmadas (por ejemplo, anuladas), se usa el promedio. El costo de lo vendido se cambió en D-54. | El precio mira hacia adelante: tiene que alcanzar para reponer. |
| D-53 | La vida útil se carga en días, semanas o meses (un mes cuenta 30 días) y se guarda en días (1 a 3650). Solo la tiene un producto perecedero; es opcional. | Un solo número compara fácil; la unidad es comodidad de la pantalla. |
| D-54 | *(Reemplazada por D-60.)* El **A2 del cierre** (RN-07) y el **consumo de la casa** (RN-11) usan el **último costo puesto**: Reposición recibe lo que cuesta volver a comprar. El **promedio ponderado** queda para valuar las **pérdidas** (merma, vencidos, roturas) y las **diferencias de conteo**, y se muestra en la ficha del producto. Rige desde el próximo cierre: los cierres ya hechos no se recalculan. | Con el promedio, cuando un costo sube, la diferencia entre lo que costó y lo que cuesta reponer se reparte como ganancia y Reposición queda corta (ganancia por inflación). Con el último costo, A3 es más conservador pero se puede repartir sin achicar el negocio. Si un costo baja pasa al revés, pero en Cuba es lo menos común. Lo perdido vale lo que costó: por eso las pérdidas siguen al promedio. |
| D-55 | Un **respaldo** es una copia completa de la base hecha por el propio SQLite (`VACUUM INTO`), **verificada** (`integrity_check`) y comprimida en un `.zip` con un `manifest.json` (fecha, motivo, última migración y huella SHA-256 de la base). Se guarda en la carpeta de la PC (`%LOCALAPPDATA%\Cuadre\backups`) y se copia a la carpeta del **pendrive**, comparando la huella de la copia con la del original. El nombre dice cuándo y por qué: `cuadre-2026-11-14_213005-cierre.zip` (hora local). Cada carpeta rota por su cuenta (RF-DAT-03), contando **días, semanas y meses que tienen respaldo**; las copias de seguridad (antes de actualizar o de restaurar) rotan aparte: quedan las últimas 5. Si el pendrive **no está conectado**, el respaldo local se hace igual y no cuenta como falla: solo el aviso de RF-DAT-06 sigue contando días. El respaldo del cierre se hace en segundo plano (el cierre no espera); el de la salida, al cerrar la ventana. | Copiar el archivo con la app abierta puede dar una copia a medias; `VACUUM INTO` da una copia consistente. Un respaldo que nunca se probó no es un respaldo: por eso se verifica al hacerlo y al restaurarlo. Un pendrive puede escribir basura sin dar error: por eso se compara la huella. |
| D-56 | La configuración y la historia de los respaldos (carpeta del pendrive, días del aviso, último respaldo local y externo, último problema) se guardan en `backup.json`, **al lado** de la base y **no dentro** de ella. | Restaurar una copia de hace 10 días no debe hacer que la app olvide que ayer se respaldó, ni dónde está el pendrive. |
| D-57 | Cuando una versión nueva de Cuadre necesita migrar la base, **antes** deja un respaldo (`antes-de-actualizar`). Si ese respaldo falla, la actualización sigue y el problema se avisa en Inicio. | Si una migración sale mal, se vuelve a la base como estaba. No arrancar dejaría al negocio sin su app. |
| D-58 | **Restaurar** (RF-DAT-05): se elige un respaldo de la lista (de la PC o del pendrive). Antes de tocar nada se comprueba que el zip sea de Cuadre, que la huella coincida, que la base esté sana y que **no sea de una versión más nueva** de la app. Después se hace una copia de lo que hay ahora (`antes-de-restaurar`, solo en la PC), se reemplazan **todos** los datos y se aplican las migraciones que le falten a una copia vieja. Lo cargado después del respaldo se pierde (queda en la copia de seguridad). | Restaurar es la operación más peligrosa de la app: se valida todo antes y siempre hay vuelta atrás. |
| D-59 | **Registro de auditoría** (RF-DAT-08): una sola lista, la más nueva primero, filtrable por días y por tipo. Muestra: **anulaciones** (compras, traslados/salidas/conteos, operaciones de sobres, gastos, recuperaciones de faltantes) con su motivo; **días reabiertos**; **cambios de precio** (no el primer precio de un producto); **planes Profit First** y **pagos del personal** nuevos, comparados con el anterior (no el primero de cada uno); y los **cambios en Configuración** (negocio y precios, cajas, formas de cobro, sobres, empleados, categorías de gasto) más las **correcciones de la tasa** (cargar la del día no es un cambio). Lo que ya se guardaba sin borrarse se lee de donde está; solo lo que antes se pisaba se guarda aparte, campo por campo, con el valor de antes y el de después **escritos como se leen** ("20 %", "Activo"), y solo si algo cambió de verdad. | No copiar lo que ya está guardado: una sola fuente de verdad. El valor se escribe al momento del cambio para que el historial se lea igual dentro de años, aunque después cambien los nombres. |
| D-60 | **Costo por tandas (FIFO).** Cada línea de una compra confirmada es una **tanda**: su costo en USD y su tasa quedan fijos. Lo que sale del estante (ventas del cierre, salidas, faltantes de conteo) se lleva **primero lo de la tanda más vieja**. Las tandas son del **producto**, no de la ubicación: los traslados no las tocan. No se guardan: salen de **repasar el historial de stock** (sin lo anulado), igual que el promedio, así que anular una compra o reabrir un día no necesita nada especial. **A2** = costo de las tandas + **USD × colchón** (el del producto o el general), guardado aparte en cada línea del cierre; el **consumo de la casa** se valúa igual. Las **pérdidas** van a lo que costaron, **sin colchón**. Un **conteo que da de más** o una **venta negativa** entran al costo de la **tanda más nueva**. Lo vendido **sin stock** (la compra todavía no se cargó) se cuesta con la última tanda conocida, y el repaso lo paga con la compra cuando llega. Lo que había antes de la primera compra toma el costo de esa compra. La ficha muestra el costo de la **tanda que se está vendiendo** (la más vieja con stock; sin stock, la más nueva). Rige desde el próximo cierre: los cierres hechos no se recalculan. Reemplaza a D-54. | Con FIFO lo vendido más lo que queda suma exactamente lo que se pagó: el contador lo puede usar tal cual. El colchón ya se cobra en el precio: con D-54 terminaba repartido como ganancia, ahora queda en Reposición para cuando suba la tasa. Lo perdido vale lo que costó: por eso las pérdidas no llevan colchón. |
| D-61 | **Precio por tanda.** El precio sugerido, el margen y la alerta del colchón usan la **tanda que se está vendiendo** (D-60). La app **nunca cambia un precio sola**: avisa, en Inicio y en la Lista de precios. **Tanda nueva en venta:** la tanda en venta vino de una compra, el precio no se decidió para ella y no coincide con su sugerido (también la primera tanda de un producto nuevo). **La próxima tanda no entra en el precio:** la compra más nueva espera detrás de la tanda en venta y su sugerido supera el precio. Cada aviso se responde con **"Usar el precio sugerido"** (rige desde hoy, como cualquier cambio de precio, y la Lista de precios lo marca para el cartel) o **"Mantener el precio"**; cualquier cambio de precio cuenta como decidido para la tanda en venta. Con el estante vacío no hay aviso. Al estrenar esto, los precios vigentes cuentan como decididos para la tanda en venta. | Si la app cambiara el precio sola, el cierre calcularía la venta con un precio que el cartel todavía no tiene y aparecerían faltantes falsos. Avisar antes de que se agote la tanda vieja deja subir a tiempo y que Reposición no quede corta. |
| D-62 | **Impuestos configurables.** Cada impuesto tiene nombre, tasa, base (**ventas** = lo cobrado, A1; o **ganancia** = la ganancia antes de impuestos del resumen del contador, nunca menos de 0) y si está activo. **Ninguno por defecto**; no se borran, se desactivan, y sus cambios quedan en el registro. Cada uno se calcula sobre su base, sin sumarse unos sobre otros. Son **solo una sugerencia** de cuánto apartar: no mueven plata y el reparto Profit First **no cambia**. | El impuesto de hoy no es fijo ni se paga siempre: así queda listo para cuando haga falta, sin tocar el método. |
| D-63 | **Resumen para el contador** (por mes). **Ingresos** por forma de cobro (el cierre solo cobra CUP, D-28). **Costo de venta al costo real por tandas (FIFO, D-60), sin el colchón**: el colchón es plata guardada en Reposición, no un costo; se muestra aparte. **Ganancia bruta** = ingresos − costo de venta. **Ganancia antes de impuestos** = bruta − gastos − personal − pérdidas de mercadería + sobrantes de conteo. Los **faltantes de caja perdidos no se restan**: los ingresos ya son lo que se cobró de verdad, así que ya faltan ahí; se muestran como dato. Después, los **impuestos sugeridos** (D-62) y la ganancia después de impuestos. **Compras** del mes (mercadería + cargos). **Inventario a costo** al empezar (el día antes del 1) y al terminar, **por ubicación**: las tandas son del producto, así que cada ubicación toma su parte por cantidad. **Lo pagado en USD** (gastos, compras) va aparte **y** convertido a CUP: los gastos con la tasa de su día (o la última cargada antes), las compras con la tasa de esa compra. Un gasto en USD sin ninguna tasa antes queda fuera de los totales en CUP, con aviso. El consumo de la casa es un retiro del dueño (RN-11): no entra. | Al contador le alcanza con el estado de rendimiento y los totales. Con FIFO lo vendido más lo que queda suma lo que se pagó, así que el costo real y el inventario cierran entre sí. Restar los faltantes dos veces haría ver menos ganancia de la que hubo. |

---

## 7. Preguntas abiertas

| # | Pregunta |
|---|---|
| ~~P-01~~ | Resuelta → D-20. |
| ~~P-02~~ | Resuelta → D-17. |
| ~~P-03~~ | Resuelta → D-18. |
| ~~P-04~~ | Resuelta → D-19. |
| P-05 | **Frecuencia del reparto:** cierre diario (decidido). ¿Se suma el ritmo del libro (días 10 y 25) para mover Ganancia e Impuestos a un lugar aparte? |
| P-06 | **Versat:** conseguir la especificación de su formato de importación. |
| P-07 | **Colchón por defecto:** ¿qué monto? ¿Varía por categoría? |
| P-08 | **Conteo del almacén:** ¿cada cuánto se hace? ¿La app debería recordarlo? |

---

## 8. Próximos pasos

1. **Propuesta de producto:** visión, usuario, problemas que resuelve, alcance de la v1 y criterios de éxito.
2. **Diseño visual (PC):** navegación, pantallas clave (tablero, cierre diario, catálogo, compras, sobres) y estilo visual.
3. Recién después: elegir el stack y empezar a programar.

---

## 9. Referencias

- `RESUMEN_PROFIT_FIRST_BODEGON.md`: análisis de los números de enero a septiembre de 2026 y problemas del Excel.
- Mike Michalowicz, *Profit First*.

---

## 10. Historial

| Versión | Fecha | Cambio |
|---|---|---|
| 1.0 | 10/11/2026 | Consolidación de todas las decisiones de la sesión de requisitos (v0.1 a v0.5). |
| 1.1 | 10/11/2026 | Resueltas P-02, P-03 y P-04: merma como pérdida (RN-06b), faltantes y sobrantes con seguimiento (RN-06c, RF-CIE-12 a 14), % del dependiente sobre A1. Nuevo RF-REP-08. |
| 1.2 | 10/11/2026 | Sin impresora: la planilla de conteo y la lista de precios se muestran en pantalla para copiarlas a mano (RF-CIE-01, RF-PRD-09); la impresión pasa a futuro. Interfaz adaptable (RNF-01b). |
| 1.3 | 12/11/2026 | Etapa 5 (sobres): resuelta P-01 y nuevas D-20 a D-25. La deuda interna (RF-PF-09) y la evaluación por período (RF-PF-10) quedan para la etapa 6, con el cierre. |
| 1.4 | 12/11/2026 | Etapa 6 (cierre diario): nuevas D-26 a D-34 (bloqueo de días cerrados, reapertura, cierre en CUP, planilla, deuda interna, faltantes, pago al dependiente). Quedan para la etapa 6b: resolver faltantes, aviso de días sin cerrar, evaluación por período (RF-PF-10) y detección de duplicados. |
| 1.5 | 12/11/2026 | Etapa 6b: nuevas D-35 a D-39 (recuperar y dar por perdido un faltante, bloqueo de la reapertura con recuperaciones, días sin cerrar, posibles duplicados, evaluación por período). La D-33 queda completa. |
| 1.6 | 13/11/2026 | Etapa 7 (gastos): nuevas D-40 a D-45 (de qué sobre sale un gasto, anulación, categorías de fábrica, gastos fijos y sus vencimientos, confirmar o saltar un vencimiento, gastos pagados del cajón). |
| 1.7 | 13/11/2026 | Etapa 8 (tablero y reportes): nuevas D-46 a D-51 (comparación del mes, qué es pérdida de mercadería, faltante perdido, ranking por venta teórica, evolución de costos y comparación de proveedores, reporte mensual con tendencia de 12 meses). RF-REP-06 queda pendiente. |
| 1.8 | 13/11/2026 | Pulido: nuevas D-52 (el precio sugerido y la alerta del colchón usan el último costo puesto; los reportes siguen con el promedio) y D-53 (vida útil). Nuevos RF-PRD-10 y RF-PRD-11. RN-03 y RN-04 actualizadas. Los deseables a futuro pasan a `REQUISITOS_V2.md`. |
| 1.9 | 14/11/2026 | Nueva D-54: el A2 del cierre (RN-07) y el consumo de la casa (RN-11) pasan al último costo puesto; el promedio queda para las pérdidas y las diferencias de conteo. D-52 y RN-03 ajustadas. |
| 1.10 | 14/11/2026 | Respaldos: nuevas D-55 (cómo se hace, verifica, nombra, copia al pendrive y rota un respaldo), D-56 (su estado vive fuera de la base), D-57 (respaldo antes de actualizar) y D-58 (reglas de la restauración). |
| 1.11 | 14/11/2026 | Exportación, parte 1: nueva D-59 (qué muestra el registro de auditoría y de dónde lo lee). |
| 1.12 | 14/11/2026 | Nueva D-60: costo por tandas (FIFO) con el colchón dentro de A2 y del consumo de la casa; las pérdidas por tandas sin colchón. Reemplaza a D-54. RN-07, RN-11 y D-47 ajustadas. |
| 1.13 | 14/11/2026 | Nueva D-61: precio sugerido, margen y alerta del colchón por la tanda en venta; avisos de tanda nueva en venta y de próxima tanda más cara, sin cambiar precios solos. Reemplaza a D-52. RN-03 y RN-04 ajustadas. |
| 1.14 | 14/11/2026 | Nuevas D-62 (impuestos configurables, solo como sugerencia) y D-63 (resumen para el contador). Nuevos RF-IMP-03 y RF-REP-09. |
