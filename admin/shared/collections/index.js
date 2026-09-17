/**
 * Registro de colecoes do admin.
 *
 * Para cadastrar algo novo (ingredientes, equipamentos...):
 *   1. crie shared/collections/<nome>.js seguindo o modelo de events.js
 *   2. importe e adicione em COLLECTIONS abaixo
 *   3. remova o item correspondente de PLANNED
 * O servidor e a interface passam a oferecer o CRUD automaticamente.
 */
import events from './events.js';

export const COLLECTIONS = [events];

/** Aparecem no menu como "em breve". */
export const PLANNED = [
  { name: 'ingredients', label: 'Ingredientes' },
  { name: 'equipments', label: 'Equipamentos', unity: 'EquipmentData' },
  { name: 'products', label: 'Produtos', unity: 'ProductData' },
  { name: 'restaurants', label: 'Restaurantes', unity: 'RestaurantData' },
  { name: 'roles', label: 'Cargos', unity: 'RoleData' },
  { name: 'creditLines', label: 'Linhas de crédito', unity: 'CreditLineData' },
  { name: 'locations', label: 'Localizações', unity: 'LocationData' },
];

export function getCollection(name) {
  return COLLECTIONS.find((c) => c.name === name) ?? null;
}
