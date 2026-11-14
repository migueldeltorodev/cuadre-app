# Requisitos deseables para la v2

> **Fecha:** 13/11/2026 · **Producto:** Cuadre
> Mejoras que **no entran en la primera beta**. Cada una se discute y se pasa a `REQUISITOS_APP.md` cuando se decida hacerla.

---

## Pendientes de la v1

| ID | Requisito | Origen |
|---|---|---|
| RF-REP-06 | Evolución de la tasa de cambio frente al colchón de cada producto. | Deseable de la etapa 8. |
| RF-PRD-08 | Importar el catálogo desde Excel. | Deseable del catálogo. |

## Vencimientos (continúa RF-PRD-10)

| ID | Requisito | Notas |
|---|---|---|
| V2-VEN-01 | Al confirmar una compra de un producto perecedero, calcular un **vencimiento estimado**: fecha de la compra + vida útil. | Necesita la vida útil (RF-PRD-10, ya en la v1). |
| V2-VEN-02 | La línea de la compra puede traer la **fecha de vencimiento real** de la etiqueta, precargada con la estimada. | Opcional por línea. |
| V2-VEN-03 | **Alerta en el Inicio:** "Puede estar por vencer", con el producto y la compra, unos días antes del vencimiento. | Los días de aviso, configurables. |
| V2-VEN-04 | Saber qué compra sigue en el estante. | Las ventas salen del conteo, no de cada unidad: hay que suponer que lo primero que entra es lo primero que sale (FIFO). Es la parte difícil. |
