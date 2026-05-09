# Requerimiento Funcional – Sistema de Finanzas Personales Multi-Usuario

---

## 1. Objetivo General

Desarrollar un sistema web que permita a **múltiples usuarios** gestionar sus finanzas personales de forma independiente, segura y eficiente. El sistema debe permitir el registro de ingresos, egresos fijos (servicios, pasivos), movimientos de tarjeta de crédito, cálculo automático de balance mensual e integración con un servicio externo de precios de supermercados (Coto y Carrefour) para planificar compras utilitarias.

Cada usuario opera en un entorno completamente aislado, sin acceso a los datos financieros de otros usuarios.

---

## 2. Alcance

El sistema permitirá a cada usuario autenticado:

- Registrar y categorizar ingresos y egresos.
- Administrar servicios de vivienda (agua, luz, gas, expensas, etc.).
- Administrar deudas y cuotas de tarjeta de crédito.
- Definir pasivos fijos mensuales (facultad, seguro, patente, etc.).
- Calcular balance automático del mes.
- Integrar con un servicio externo de precios de Coto y Carrefour.
- Planificar compras necesarias según saldo disponible.
- Registrar múltiples usuarios con datos completamente aislados.
- Contar con roles básicos (usuario estándar y administrador).

---

## 3. Requerimientos Funcionales Generales

### 3.1 Gestión de Servicios de Vivienda

| ID | Requerimiento |
|----|----------------|
| RF-01 | El sistema debe permitir registrar servicios básicos (agua, luz, gas, internet, expensas, ABL, etc.) con nombre, monto, fecha de vencimiento y periodicidad (mensual, bimestral, etc.). |
| RF-02 | El sistema debe calcular automáticamente el total de servicios del mes actual para el usuario logueado. |
| RF-03 | El usuario debe poder marcar un servicio como "pagado" y registrar fecha de pago. |
| RF-04 | Los servicios deben poder editarse y eliminarse solo por su propietario. |

### 3.2 Gestión de Tarjeta de Crédito

| ID | Requerimiento |
|----|----------------|
| RF-05 | El sistema debe permitir registrar compras en cuotas (ej: 3, 6, 12 cuotas) indicando monto total, cantidad de cuotas, cuota actual e interés si aplica. |
| RF-06 | El sistema debe mostrar el total a pagar en el mes actual por cuotas de compras anteriores. |
| RF-07 | El sistema debe permitir registrar compras corrientes con tarjeta de crédito que vencen en el mes actual (pago en una sola cuota). |
| RF-08 | El sistema debe diferenciar entre: compras en cuotas (solo la cuota del mes) y compras al contado con tarjeta. |
| RF-09 | El total de tarjeta de crédito del mes debe incluir: suma de cuotas activas + compras corrientes. |
| RF-10 | Cada usuario solo puede ver y gestionar sus propias compras con tarjeta. |

### 3.3 Gestión de Pasivos

| ID | Requerimiento |
|----|----------------|
| RF-11 | El sistema debe permitir definir pasivos fijos no evitables (facultad, seguro del auto, patente, préstamos personales, cuota del gimnasio, etc.) con nombre, monto mensual y vencimiento. |
| RF-12 | El cálculo total de **Pasivos** debe ser: <br> `Total Pasivos = Total Servicios (RF-02) + Total Tarjeta del mes (RF-09) + Total Otros Pasivos Fijos (RF-11)` |
| RF-13 | El usuario debe poder visualizar el desglose de pasivos por categoría (servicios, tarjeta, fijos). |

### 3.4 Balance General

| ID | Requerimiento |
|----|----------------|
| RF-14 | El sistema debe permitir registrar ingresos (sueldo, transferencias, devoluciones, honorarios, etc.) con detalle, monto, fecha y categoría opcional. |
| RF-15 | El balance se calcula como: <br> `Saldo Restante = Total Ingresos – Total Pasivos – Otros Egresos Opcionales` <br> Donde "Otros Egresos Opcionales" incluye compras de dólares, ahorros, inversiones, etc. |
| RF-16 | El sistema debe mostrar un resumen mensual con el siguiente formato: <br><br> `DETALLE                     IMPORTE` <br> `INGRESO DE CAPITAL          $ 1.400.000,00` <br> `PASIVOS TOTALES             -$ 470.496,00` <br> `OTROS EGRESOS               -$ 560.000,00` (ej: compra dólares) <br> `============================ ===========` <br> `SALDO RESTANTE              $ 369.504,00` |
| RF-17 | El saldo restante debe usarse como base para calcular disponibilidad para compras de utilitarios (supermercado). |

### 3.5 Integración con Servicio Externo de Precios (Coto y Carrefour)

| ID | Requerimiento |
|----|----------------|
| RF-18 | El sistema debe consumir un servicio REST existente (previamente desarrollado por un tercero) que permite consultar precios de productos en supermercados Coto y Carrefour. |
| RF-19 | El usuario debe poder buscar productos por nombre (ej: "leche", "arroz", "carne", "jabón") y ver el precio más bajo disponible entre ambos supermercados. |
| RF-20 | El sistema debe permitir armar una "lista de compras necesarias del mes" (utilitarios: alimentos, limpieza, higiene personal). |
| RF-21 | El sistema debe calcular el costo estimado total de la lista de compras según precios actuales del servicio externo. |
| RF-22 | El sistema debe comparar el costo total de la lista de compras con el saldo restante del balance (RF-16) y mostrar una alerta si supera el disponible. |
| RF-23 | El usuario debe poder simular qué productos puede comprar según el saldo restante (recomendación automática de prioridades). |
| RF-24 | Cada usuario tiene sus propias listas de compras, independientes de las de otros usuarios. |

### 3.6 Reportes y Visualización

| ID | Requerimiento |
|----|----------------|
| RF-25 | El sistema debe mostrar un dashboard mensual con: ingresos totales, pasivos totales, saldo restante, y desglose de servicios/tarjeta/pasivos fijos. |
| RF-26 | El usuario debe poder ver histórico por mes (últimos 12 meses como mínimo). |
| RF-27 | El sistema debe permitir exportar el resumen del mes a CSV o PDF. |
| RF-28 | Los reportes solo incluirán datos del usuario autenticado que realiza la exportación. |

---

## 4. Requerimientos Multi-Usuario

### 4.1 Gestión de Usuarios y Autenticación

| ID | Requerimiento |
|----|----------------|
| MU-01 | El sistema debe permitir el registro de nuevos usuarios con email válido y contraseña segura (mínimo 8 caracteres, 1 mayúscula, 1 número). |
| MU-02 | El sistema debe autenticar usuarios mediante email y contraseña, generando un token de sesión (JWT o similar). |
| MU-03 | El sistema debe permitir la recuperación de contraseña vía email (envío de enlace temporal). |
| MU-04 | El sistema debe permitir al usuario cerrar sesión (logout). |
| MU-05 | Cada usuario debe tener un perfil configurable con: nombre completo, email, moneda principal (ARS, USD, etc.) y fecha de registro. |
| MU-06 | No se permite registrar el mismo email más de una vez. |

### 4.2 Aislamiento de Datos por Usuario

| ID | Requerimiento |
|----|----------------|
| MU-07 | Cada usuario solo puede ver, crear, modificar o eliminar sus propios datos financieros (servicios, tarjetas, pasivos, ingresos, compras, listas de supermercado). |
| MU-08 | Toda consulta a la base de datos debe incluir un filtro por `user_id` para evitar fugas de información. |
| MU-09 | El cálculo de balance mensual debe ser exclusivo para el usuario autenticado. |
| MU-10 | Un usuario no puede acceder al dashboard ni a los datos de otro usuario, ni siquiera por manipulación manual de URLs o parámetros de API. |

### 4.3 Roles y Permisos

| ID | Requerimiento |
|----|----------------|
| MU-11 | El sistema debe tener al menos dos roles: `USER` (estándar) y `ADMIN`. |
| MU-12 | El rol `ADMIN` puede ver estadísticas globales (cantidad de usuarios activos, total de ingresos sumados de todos los usuarios, promedio de saldo restante), pero **no** puede ver datos detallados individuales sin consentimiento explícito del usuario (por defecto no se permite). |
| MU-13 | El rol `ADMIN` puede habilitar o deshabilitar cuentas de usuarios. |
| MU-14 | Por defecto, los nuevos usuarios se crean con rol `USER`. Solo un usuario con rol `ADMIN` puede promover a otro usuario a `ADMIN`. |

### 4.4 Seguridad y Sesiones

| ID | Requerimiento |
|----|----------------|
| MU-15 | Las sesiones deben expirar automáticamente después de 8 horas de inactividad. |
| MU-16 | Las contraseñas deben almacenarse hasheadas (bcrypt o Argon2). |
| MU-17 | El sistema debe usar HTTPS en entornos productivos. |
| MU-18 | Debe registrarse en un log la fecha y hora del último acceso de cada usuario. |
| MU-19 | El sistema debe permitir la eliminación de cuenta propia por parte del usuario, con confirmación y eliminación en cascada de todos sus datos financieros. |

---

## 5. Requerimientos No Funcionales

| ID | Requerimiento |
|----|----------------|
| NF-01 | El sistema debe responder en menos de 2 segundos para operaciones habituales (consulta de balance, registro de gasto). |
| NF-02 | La integración con el servicio de precios de supermercados debe tener un timeout máximo de 3 segundos y manejar fallas sin caída del sistema (mostrar mensaje amigable). |
| NF-03 | El sistema debe estar disponible al menos el 99% del tiempo (en producción). |
| NF-04 | El código debe ser mantenible y escalable horizontalmente para soportar al menos 1000 usuarios concurrentes. |
| NF-05 | La interfaz debe ser responsive (funcional en móviles, tablets y desktop). |

---

## 6. Supuestos y Restricciones

- Las definiciones de los endpoints del servicio el cual va a exponer los precios, categorias y productos de carrefour y coto todavia les falta especificacion, no implementar directamente la consulta de estos mismos sino ultilizar mockup data.


---

## 7. Criterios de Aceptación Generales

- ✅ Un usuario puede registrarse, loguearse y cerrar sesión correctamente.
- ✅ Dos usuarios distintos no pueden ver ni modificar los datos del otro.
- ✅ El cálculo de pasivos y balance se actualiza automáticamente al agregar/editar/eliminar cualquier registro.
- ✅ La integración con el servicio de precios devuelve resultados válidos y se usa correctamente para planificar compras.
- ✅ El usuario puede armar una lista de compras y saber si alcanza con su saldo restante.
- ✅ El administrador puede ver estadísticas globales y deshabilitar usuarios.
- ✅ Un usuario que elimina su cuenta desaparece completamente del sistema (excepto logs anonimizados si existen por política).

---

## 8. Posibles Extensiones Futuras (Fuera del alcance inicial)

- Compartir finanzas entre usuarios familiares (gastos comunes).
- Múltiples monedas con conversión automática.
- Notificaciones por email de vencimientos de servicios y tarjeta.
- Categorías personalizables y presupuestos por categoría.
- Gráficos interactivos de evolución de ahorro.

---