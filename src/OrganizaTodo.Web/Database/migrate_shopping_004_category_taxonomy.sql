-- ============================================================
-- SHOPPING DB MIGRATION 004 — Category taxonomy (parent/subcategory)
-- Run this against POSTGRES_DB_Shopping, NOT the main organizaTodo DB
--
-- Uses ~* (POSIX case-insensitive regex). Order of CASE branches matters:
-- more specific patterns first to prevent false matches.
-- ============================================================

CREATE TABLE IF NOT EXISTS category_taxonomy (
    subcategory      TEXT PRIMARY KEY,
    parent_category  TEXT NOT NULL
);

TRUNCATE category_taxonomy;

INSERT INTO category_taxonomy (subcategory, parent_category)
SELECT DISTINCT norm_cat,
    CASE
        -- ── INFUSIONES (before Bebidas — café/mate/té would also match agua) ──
        WHEN norm_cat ~* 'café|cafe|cápsula|capsula|yerba|mate |infusion|té |te |tostado|expresso|espresso|manzanilla|cacao en polvo|cacao|instantáneo de café'
             THEN 'Infusiones'

        -- ── BEBIDAS ALCOHÓLICAS ──────────────────────────────────────────
        WHEN norm_cat ~* 'vino|cerveza|cervezas|champa|espumante|aperitivo|amargo|licor|whisky|vodka|ginebra|chopp|ron |rum|fernet|sidra|bitter|americano|aperitivos con alcohol|aperitivos sin alcohol|mistela'
             THEN 'Bebidas Alcohólicas'

        -- ── BEBIDAS (non-alcoholic, with exclusions for false positives) ─
        WHEN norm_cat ~* 'agua|aguas|gaseosa|gaseosas|soda|jugo|jugos|isotón|isoton|energizante|bebidas?'
             AND norm_cat !~* 'aguarras|galletitas de agua|juego[s]? de agua|purificad|bolsa[s]?|filtro'
             AND norm_cat !~* 'alcohol|cerveza'
             THEN 'Bebidas'

        -- ── MASCOTAS (before Almacén — "alimentos" appears in both) ─────
        WHEN norm_cat ~* 'para gatos|para perros|mascota|alimentos secos para|alimentos húmedos para|snack para|arena para gato|collar para|correa|accesorio[s]? para mascota'
             THEN 'Mascotas'

        -- ── LÁCTEOS Y HUEVOS ─────────────────────────────────────────────
        WHEN norm_cat ~* 'leche|lácteo|lacteo|yogur|yogures|queso|quesos|crema de leche|cremas de leche|manteca|margarina|mantecas|dulce de leche|arroz con leche|huevo|huevos|ricota|muzarela|mozzarela'
             THEN 'Lácteos y Huevos'

        -- ── CARNES Y PESCADOS ─────────────────────────────────────────────
        WHEN norm_cat ~* 'carne|filete|filetes|marisco|atún|atun|bondiola|achura|embutido|vacuna|pollo|cerdo|pescado|rebozado|hamburguesa|salchich|panceta|chorizo'
             THEN 'Carnes y Pescados'

        -- ── FRUTAS Y VERDURAS ─────────────────────────────────────────────
        WHEN norm_cat ~* 'fruta|frutas|verdura|verduras|vegetal|vegetales|arveja|brócoli|brocoli|chaucha|choclo|tomate[s]?|papa |papas |manzana|naranja|limón|limon|zanahoria|cebolla|lechuga|espinaca|banana|pera |acelga|pimiento|zapallo|alimento[s]? vegetal'
             THEN 'Frutas y Verduras'

        -- ── GOLOSINAS Y SNACKS (before Almacén to catch galletitas, chocolates) ──
        WHEN norm_cat ~* 'alfajor|galletita|galletitas|chocolate|barras de chocolate|chocolates|caramelo|caramelos|chicle|chicles|gomita|gomitas|chupetín|chupetin|chupetines|bombón|bombon|bombones|bocadito|bocaditos|bizcochito|papas fritas|snack|chips|huevo de pascua|huevos de pascua|turron|turrón|chip cookie|brownie|barrita|barritas|bombones y bocadito|caramelos, gomitas|con relleno|con sal|confite[s]?|turrón'
             THEN 'Golosinas y Snacks'

        -- ── ALMACÉN / DESPENSA ────────────────────────────────────────────
        WHEN norm_cat ~* 'arroz|fideos|pasta |pastas |capelletini|harina|harinas|aceite|aceites|aceitunas|aceto|azúcar|azucar|conserva|legumbre|arvejas|lentejas|garbanzos|mermelada|salsa|salsas|aderezos|aderezo|vinagre|caldo|caldos|cereal[es]?|avena|semola|sémola|granola|miel |almidón|almidon|maíz|maiz|premezcla|levadura|gelatinas?|cobertura|polenta|condimento|especias|especia|hierbas secas|sal |aceto|cobertura|relleno|coberturas|comida[s]? congelada|comida[s]? instantánea|comida[s]? y panificados'
             THEN 'Almacén'

        -- ── PANADERÍA Y REPOSTERÍA ────────────────────────────────────────
        WHEN norm_cat ~* 'pan |panes|lacteado|bizcochuelo|torta |facturas|medialunas|tostada|crackers|tapa|tapas y pasta|repostería|pastelería|budín|budin'
             THEN 'Panadería y Repostería'

        -- ── BEBÉ Y NIÑOS ──────────────────────────────────────────────────
        WHEN norm_cat ~* 'bebé|bebe|pañal|pañales|mamadera|chupete|andador|cereal infantil|alimento infantil|colonia para bebé|bebes/bebotes|a partir 1 año|a partir 6 mes|infantil|bebes|carrito'
             THEN 'Bebé y Niños'

        -- ── JUGUETES Y ENTRETENIMIENTO ────────────────────────────────────
        WHEN norm_cat ~* 'juguete|muñeca|muñecas|muñeco|muñecos|peluche|peluches|pelota[s]?|juego de mesa|juegos de mesa|didáctico|didactico|armado|encastre|figuras?|radio control|burbujero|casita y juego|aro para basquet|ciencia|set de juego|vehículos de radio|vehiculos de radio|bloque/armado|canastas y bloque|arco |criquet|danbo|creatividad|canastas|juego[s]? de agua| auto '
             THEN 'Juguetes y Entretenimiento'

        -- ── LIBRERÍA Y PAPELERÍA ──────────────────────────────────────────
        WHEN norm_cat ~* 'cuaderno|cuadernos|carpeta[s]?|cartuchera[s]?|lápiz|lapiz|lapices|bolígrafo|boligrafo|boligrafos|marcador[es]?|resaltador[es]?|bloc |bloques para mochila|repuesto de hoja|adhesivo[s]? escolar|acuarela[s]?|pintura escolar|librería|libreria|agend|abrochadoras|accesorios de oficina|corrector[es]?|crayón|crayon|crayones|goma de borrar|tijera[s]?'
             THEN 'Librería y Papelería'

        -- ── HIGIENE PERSONAL ──────────────────────────────────────────────
        WHEN norm_cat ~* 'shampoo[s]?|acondicionador[es]?|jabón de tocador|jabones en barra|jabón líquido|jabón liquido|crema[s]? dental|cepillo[s]? dental|papel higiénico|papeles higiénico|papel higienico|antiséptico bucal|antitranspirante[s]?|desodorante[s]?|protector[es]? diario|afeitado|coloración|coloracion|algodón|algodon|hisopo|hisopos|botiquín|botiquin|depilación|depilacion|tampón|tampon|femenin|toallita[s]? húmeda|máscara|mascara|delineador|maquillaj|base de maquill|gel de baño|crema corporal|crema hidrat|cuidado de los pies|cortabarba|aplicador|cepillo de diente[s]?|cepillos de diente'
             THEN 'Higiene Personal'

        -- ── LIMPIEZA DEL HOGAR ────────────────────────────────────────────
        WHEN norm_cat ~* 'detergente[s]?|lavandina[s]?|jabón para la ropa|jabones para la ropa|suavizante[s]?|esponja[s]?|bolsas de residuos|trapo de piso|paño[s]?|escoba[s]?|limpiador[es]?|abrillantador|autobrillo|ceras para piso|barrefondo|desengrasante|quitasarro|quitamanchas|prelavado|aerosol limpia|en aerosol|antideslizante|lavaplatos|cloro|amoniaco|tronador|accesorios de limpieza|bolsas herméticas|bolsas para freezer|bolsa[s]? de compra|cepillos escobillas|aerosol$| aerosol'
             THEN 'Limpieza del Hogar'

        -- ── FERRETERÍA Y PINTURA ──────────────────────────────────────────
        WHEN norm_cat ~* 'latex|barniz|pintura|antioxido|aisladora|aguarras|cemento|masilla|sellador|20 litros|21 a 40|herramienta[s]?|caja de herramienta|alicate|amoladora|atornillador|sierra|circular|caladora|bordeadora|calibre|cinta métrica|cinta metrica|cerradura[s]?|candado[s]?|cañería|cañeria|carretel|burletes|taladro|llave de paso|tubería|grifería|buscapolo|compresor|caja de corte|cortadora cesped|cutter|balde[s]?|azul$|rojo$|blanco$|amarillo$|verde$|negro$|gris$'
             THEN 'Ferretería y Pintura'

        -- ── ELECTRÓNICA ───────────────────────────────────────────────────
        WHEN norm_cat ~* 'televisor| tv |smart tv|soportes tv|parlante[s]?|auricular[es]?|celular[es]?|tablet|computadora|notebook|router|cámara|camara|microonda|horno eléctrico|hornos eléctrico|calefacción|termotanque|calefón|calefon|anafe|aspirador[a]?|heladera|lavarropas|secarropas|aire acondicionado|ventilador[es]?|cafetera[s]?|licuadora[s]?|batidora[s]?|procesadora|plancha|tostadora[s]?|pava eléctrica|audio para auto|antena|impresora|teclado|bateria|baterias|cargador|convertidor|consola[s]?|control remoto|barras de sonido|arrocera|cocina[s]? eléctrica|cocción|balanza[s]? de baño|alambrico|cabos|accesorios y cables'
             THEN 'Electrónica'

        -- ── AUTOMOTOR ─────────────────────────────────────────────────────
        WHEN norm_cat ~* 'automotor|bujía|limpiaparabrisas|baliza| auto$|^auto$'
             THEN 'Automotor'

        -- ── INDUMENTARIA ──────────────────────────────────────────────────
        WHEN norm_cat ~* 'campera[s]?|chaleco[s]?|abrigo|buzos|canguro|sweater|remera|pantalón|pantalon|calzado|zapatilla|zapato|indumentaria|sintetico|sintético|polar|buzo'
             THEN 'Indumentaria'

        -- ── HOGAR ─────────────────────────────────────────────────────────
        WHEN norm_cat ~* 'almohada|banqueta[s]?|bandeja|vajilla|cubertería|cava|cuna|cajas.orden|mueble|sillón|silla|colchon|colchón|frazada|sábana|toalla|mantel|cocina y hogar|accesorios de parrilla|bolsas de dormir|caja fuerte|canastas?$'
             THEN 'Hogar'

        ELSE 'Otros'
    END AS parent_category
FROM (
    SELECT DISTINCT INITCAP(LOWER(TRIM(category))) AS norm_cat
    FROM carrefour_products
    UNION
    SELECT DISTINCT INITCAP(LOWER(TRIM(category))) AS norm_cat
    FROM coto_products
) all_cats;

-- ── SP functions ─────────────────────────────────────────────────────────────

DROP FUNCTION IF EXISTS sp_products_get_parent_categories();
CREATE OR REPLACE FUNCTION sp_products_get_parent_categories()
RETURNS SETOF TEXT LANGUAGE sql AS $$
    SELECT parent_category FROM (
        SELECT DISTINCT ct.parent_category,
            CASE ct.parent_category
                WHEN 'Almacén'                    THEN 1
                WHEN 'Bebidas'                    THEN 2
                WHEN 'Bebidas Alcohólicas'        THEN 3
                WHEN 'Infusiones'                 THEN 4
                WHEN 'Lácteos y Huevos'           THEN 5
                WHEN 'Carnes y Pescados'          THEN 6
                WHEN 'Frutas y Verduras'          THEN 7
                WHEN 'Panadería y Repostería'     THEN 8
                WHEN 'Golosinas y Snacks'         THEN 9
                WHEN 'Higiene Personal'           THEN 10
                WHEN 'Limpieza del Hogar'         THEN 11
                WHEN 'Bebé y Niños'               THEN 12
                WHEN 'Juguetes y Entretenimiento' THEN 13
                WHEN 'Librería y Papelería'       THEN 14
                WHEN 'Indumentaria'               THEN 15
                WHEN 'Electrónica'                THEN 16
                WHEN 'Ferretería y Pintura'       THEN 17
                WHEN 'Mascotas'                   THEN 18
                WHEN 'Automotor'                  THEN 19
                WHEN 'Hogar'                      THEN 20
                WHEN 'Otros'                      THEN 21
                ELSE 99
            END AS sort_order
        FROM category_taxonomy ct
        WHERE ct.subcategory IN (
            SELECT DISTINCT INITCAP(LOWER(TRIM(category))) FROM carrefour_products
            UNION
            SELECT DISTINCT INITCAP(LOWER(TRIM(category))) FROM coto_products
        )
    ) ranked
    ORDER BY sort_order
$$;

DROP FUNCTION IF EXISTS sp_products_get_subcategories(text);
CREATE OR REPLACE FUNCTION sp_products_get_subcategories(p_parent TEXT)
RETURNS SETOF TEXT LANGUAGE sql AS $$
    SELECT DISTINCT ct.subcategory
    FROM category_taxonomy ct
    WHERE ct.parent_category = p_parent
      AND ct.subcategory IN (
          SELECT DISTINCT INITCAP(LOWER(TRIM(category))) FROM carrefour_products
          UNION
          SELECT DISTINCT INITCAP(LOWER(TRIM(category))) FROM coto_products
      )
    ORDER BY 1
$$;

DROP FUNCTION IF EXISTS sp_products_get_by_parent_category(text);
CREATE OR REPLACE FUNCTION sp_products_get_by_parent_category(p_parent TEXT)
RETURNS TABLE(
    "Name"      TEXT,
    "Brand"     TEXT,
    "Category"  TEXT,
    "Price"     NUMERIC,
    "ListPrice" NUMERIC,
    "Promo"     TEXT,
    "Available" BOOLEAN,
    "Source"    TEXT,
    "ScrapedAt" TIMESTAMP
) LANGUAGE sql AS $$
    SELECT
        c.name, c.brand,
        INITCAP(LOWER(TRIM(c.category))),
        c.price, c.list_price,
        NULL::TEXT, c.available, 'Carrefour'::TEXT, c.scraped_at::TIMESTAMP
    FROM carrefour_products c
    JOIN category_taxonomy ct ON ct.subcategory = INITCAP(LOWER(TRIM(c.category)))
    WHERE ct.parent_category = p_parent
    UNION ALL
    SELECT
        t.name, t.brand,
        INITCAP(LOWER(TRIM(t.category))),
        t.price, t.list_price,
        t.promo, t.available, 'Coto'::TEXT, t.scraped_at::TIMESTAMP
    FROM coto_products t
    JOIN category_taxonomy ct ON ct.subcategory = INITCAP(LOWER(TRIM(t.category)))
    WHERE ct.parent_category = p_parent
    ORDER BY 1
$$;
