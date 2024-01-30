import { defu } from 'defu';
import qs from 'qs';
import { type PartialDeep } from 'type-fest';
import {
  weaponProficiencyPointsForLevelCoefs,
  weaponProficiencyPointsForAgility,
  weaponProficiencyPointsForWeaponMasterCoefs,
  experienceForLevelCoefs,
  defaultStrength,
  defaultAgility,
  defaultHealthPoints,
  healthPointsForStrength,
  healthPointsForIronFlesh,
  defaultAttributePoints,
  attributePointsPerLevel,
  defaultSkillPoints,
  skillPointsPerLevel,
  minimumLevel,
  maximumLevel,
  minimumRetirementLevel,
  experienceMultiplierByGeneration,
  maxExperienceMultiplierForGeneration,
  respecializePriceForLevel30,
  respecializePriceHalfLife,
  freeRespecializeIntervalDays,
  freeRespecializePostWindowHours,
  damageFactorForPowerStrike,
  damageFactorForPowerDraw,
  damageFactorForPowerThrow,
} from '@root/data/constants.json';

import {
  type Character,
  type CharacterCharacteristics,
  type CharacteristicConversion,
  type CharacterStatistics,
  type CharacterSpeedStats,
  type UpdateCharacterRequest,
  type EquippedItem,
  type EquippedItemId,
  CharacterClass,
  type CharacterOverallItemsStats,
  type CharacterArmorOverall,
  CharacterArmorOverallKey,
  type CharacterLimitations,
  type CharacterRating,
  type CharacteristicKey,
} from '@/models/character';
import { ItemSlot, ItemType, type Item, type ItemArmorComponent } from '@/models/item';
import { type HumanDuration } from '@/models/datetime';
import { type ActivityLog, type CharacterEarnedMetadata } from '@/models/activity-logs';
import { type TimeSeries } from '@/models/timeseries';

import { get, put, del } from '@/services/crpg-client';
import { armorTypes, computeAverageRepairCostPerHour } from '@/services/item-service';
import { t } from '@/services/translate-service';

import { applyPolynomialFunction, clamp, roundFLoat } from '@/utils/math';
import { computeLeftMs, parseTimestamp } from '@/utils/date';
import { range, groupBy } from '@/utils/array';

export const getCharacters = () => get<Character[]>('/users/self/characters');

export const getCharactersByUserId = (userId: number) =>
  get<Character[]>(`/users/${userId}/characters`);

export const updateCharacter = (characterId: number, req: UpdateCharacterRequest) =>
  put<Character>(`/users/self/characters/${characterId}`, req);

export const activateCharacter = (characterId: number, active: boolean) =>
  put(`/users/self/characters/${characterId}/active`, { active });

export const respecializeCharacter = (characterId: number) =>
  put<Character>(`/users/self/characters/${characterId}/respecialize`);

export const tournamentLevelThreshold = 20;

export const canSetCharacterForTournamentValidate = (character: Character) =>
  !(
    character.forTournament ||
    character.generation > 0 ||
    character.level >= tournamentLevelThreshold
  );

export const setCharacterForTournament = (characterId: number) =>
  put<Character>(`/users/self/characters/${characterId}/tournament`);

export const canRetireValidate = (level: number) => level >= minimumRetirementLevel;

export const retireCharacter = (characterId: number) =>
  put<Character>(`/users/self/characters/${characterId}/retire`);

export const deleteCharacter = (characterId: number) =>
  del(`/users/self/characters/${characterId}`);

export const getCharacterStatistics = (characterId: number) =>
  get<CharacterStatistics>(`/users/self/characters/${characterId}/statistics`);

export enum CharacterEarningType {
  'Exp' = 'Exp',
  'Gold' = 'Gold',
}

// TODO: spec
export const getCharacterEarningStatistics = async (
  characterId: number,
  type: CharacterEarningType,
  from: Date
) => {
  return (
    await get<ActivityLog<CharacterEarnedMetadata>[]>(
      `/users/self/characters/${characterId}/earning-statistics?${qs.stringify({ from })}`
    )
  ).reduce((out, l) => {
    const currentEl = out.find(el => el.name === t(`game-mode.${l.metadata.gameMode}`));

    if (currentEl) {
      currentEl.data.push([
        l.createdAt,
        parseInt(type === CharacterEarningType.Exp ? l.metadata.experience : l.metadata.gold, 10),
      ]);
    } else {
      out.push({
        name: t(`game-mode.${l.metadata.gameMode}`),
        data: [
          [
            l.createdAt,
            parseInt(
              type === CharacterEarningType.Exp ? l.metadata.experience : l.metadata.gold,
              10
            ),
          ],
        ],
      });
    }

    return out;
  }, [] as TimeSeries[]);
};

export const getCharacterRating = (characterId: number) =>
  get<CharacterRating>(`/users/self/characters/${characterId}/rating`);

export const getCharacterLimitations = (characterId: number) =>
  get<CharacterLimitations>(`/users/self/characters/${characterId}/limitations`);

export const getCharacterCharacteristics = (characterId: number) =>
  get<CharacterCharacteristics>(`/users/self/characters/${characterId}/characteristics`);

export const convertCharacterCharacteristics = (
  characterId: number,
  conversion: CharacteristicConversion
) =>
  put<CharacterCharacteristics>(`/users/self/characters/${characterId}/characteristics/convert`, {
    conversion,
  });

export const updateCharacterCharacteristics = (
  characterId: number,
  req: CharacterCharacteristics
) => put<CharacterCharacteristics>(`/users/self/characters/${characterId}/characteristics`, req);

const computeExperienceDistribution = (level: number): number => {
  const [a, b] = experienceForLevelCoefs;
  return Math.pow(level - 1, a) + Math.pow(b, a / 2.0) * (level - 1);
};

export const getExperienceForLevel = (level: number): number => {
  if (level <= 0) return 0;

  if (level <= 30) {
    const experienceForLevel30 = 4420824;
    return Math.trunc(
      (experienceForLevel30 * computeExperienceDistribution(level)) /
        computeExperienceDistribution(30)
    );
  }

  return getExperienceForLevel(30) * Math.pow(2, level - 30);
};

export const attributePointsForLevel = (level: number): number => {
  if (level <= 0) level = minimumLevel;
  return defaultAttributePoints + (level - 1) * attributePointsPerLevel;
};

export const skillPointsForLevel = (level: number): number => {
  if (level <= 0) level = minimumLevel;
  return defaultSkillPoints + (level - 1) * skillPointsPerLevel;
};

export const wppForLevel = (level: number): number =>
  Math.floor(applyPolynomialFunction(level, weaponProficiencyPointsForLevelCoefs));

export const wppForAgility = (agility: number): number =>
  agility * weaponProficiencyPointsForAgility;

export const wppForWeaponMaster = (weaponMaster: number): number =>
  Math.floor(applyPolynomialFunction(weaponMaster, weaponProficiencyPointsForWeaponMasterCoefs));

export const createEmptyCharacteristic = (): CharacterCharacteristics => ({
  attributes: {
    points: 0,
    strength: 0,
    agility: 0,
  },
  skills: {
    points: 0,
    ironFlesh: 0,
    powerStrike: 0,
    powerDraw: 0,
    powerThrow: 0,
    athletics: 0,
    riding: 0,
    weaponMaster: 0,
    mountedArchery: 0,
    shield: 0,
  },
  weaponProficiencies: {
    points: 0,
    oneHanded: 0,
    twoHanded: 0,
    polearm: 0,
    bow: 0,
    throwing: 0,
    crossbow: 0,
  },
});

export const createCharacteristics = (
  payload?: PartialDeep<CharacterCharacteristics>
): CharacterCharacteristics => defu(payload, createEmptyCharacteristic());

export const createDefaultCharacteristic = (): CharacterCharacteristics =>
  createCharacteristics({
    attributes: {
      points: defaultAttributePoints,
      strength: defaultStrength,
      agility: defaultAgility,
    },
    skills: {
      points: defaultSkillPoints,
    },
    weaponProficiencies: {
      points: wppForLevel(minimumLevel),
    },
  });

export const characteristicBonusByKey: Partial<
  Record<CharacteristicKey, { value: number; style: 'percent' | 'decimal' }>
> = {
  strength: {
    value: healthPointsForStrength,
    style: 'decimal',
  },
  ironFlesh: {
    value: healthPointsForIronFlesh,
    style: 'decimal',
  },
  powerStrike: {
    value: damageFactorForPowerStrike,
    style: 'percent',
  },
  powerDraw: {
    value: damageFactorForPowerDraw,
    style: 'percent',
  },
  powerThrow: {
    value: damageFactorForPowerThrow,
    style: 'percent',
  },
};

export const computeHealthPoints = (ironFlesh: number, strength: number): number =>
  defaultHealthPoints + ironFlesh * healthPointsForIronFlesh + strength * healthPointsForStrength;

// TODO: unit?
export const computeSpeedStats = (
  strength: number,
  athletics: number,
  agility: number,
  totalEncumbrance: number,
  longestWeaponLength: number
): CharacterSpeedStats => {
  const awfulScaler = 3231477.548;
  const weightReductionPolynomialFactor = [
    30 / awfulScaler,
    0.00005 / awfulScaler,
    1000000 / awfulScaler,
    0,
  ];
  const weightReductionFactor =
    1 / (1 + applyPolynomialFunction(strength - 3, weightReductionPolynomialFactor));
  const freeWeight = 2.5 * (1 + (strength - 3) / 30);
  const perceivedWeight = Math.max(totalEncumbrance - freeWeight, 0) * weightReductionFactor;
  const nakedSpeed = 0.58 + (0.034 * (20 * athletics + 2 * agility)) / 26.0;
  const currentSpeed = clamp(
    nakedSpeed * Math.pow(361 / (361 + Math.pow(perceivedWeight, 5)), 0.055),
    0.1,
    1.5
  );
  const maxWeaponLength = Math.min(
    45 + (strength - 3) * 9 + Math.pow(Math.min(strength - 3, 24) * 0.14677993, 12),
    650
  );
  const timeToMaxSpeedWeaponLenghthTerm = Math.max(
    (1.2 * (longestWeaponLength - maxWeaponLength)) / maxWeaponLength,
    0
  );

  const timeToMaxSpeed =
    0.8 *
      (1 + perceivedWeight / 15) *
      (20 / (20 + Math.pow((20 * athletics + 3 * agility) / 120, 2))) +
    timeToMaxSpeedWeaponLenghthTerm;

  const movementSpeedPenaltyWhenAttacking =
    100 * (Math.min(0.8 + (0.2 * (maxWeaponLength + 1)) / (longestWeaponLength + 1), 1) - 1);

  return {
    weightReductionFactor,
    freeWeight,
    perceivedWeight,
    nakedSpeed,
    currentSpeed,
    timeToMaxSpeed,
    maxWeaponLength,
    movementSpeedPenaltyWhenAttacking,
  };
};

export const getCharacterItems = async (characterId: number) =>
  get<EquippedItem[]>(`/users/self/characters/${characterId}/items`);

export const updateCharacterItems = (characterId: number, items: EquippedItemId[]) =>
  put<EquippedItem[]>(`/users/self/characters/${characterId}/items`, { items });

export const computeOverallPrice = (items: Item[]) =>
  items.reduce((total, item) => total + item.price, 0);

export const computeOverallWeight = (items: Item[]) =>
  items
    .filter(item => ![ItemType.Mount, ItemType.MountHarness].includes(item.type))
    .reduce(
      (total, item) =>
        (total += [ItemType.Arrows, ItemType.Bolts, ItemType.Bullets, ItemType.Thrown].includes(
          item.type
        )
          ? roundFLoat(item.weight * item.weapons[0].stackAmount)
          : item.weight),
      0
    );

interface OverallArmor extends Omit<ItemArmorComponent, 'materialType' | 'familyType'> {
  mountArmor: number;
}

export const computeOverallArmor = (items: Item[]): OverallArmor =>
  items.reduce(
    (total, item) => {
      if (item.type === ItemType.MountHarness) {
        total.mountArmor = item.armor!.bodyArmor;
      } else if (armorTypes.includes(item.type)) {
        total.headArmor += item.armor!.headArmor;
        total.bodyArmor += item.armor!.bodyArmor;
        total.armArmor += item.armor!.armArmor;
        total.legArmor += item.armor!.legArmor;
      }
      return total;
    },
    {
      headArmor: 0,
      bodyArmor: 0,
      armArmor: 0,
      legArmor: 0,
      mountArmor: 0,
    }
  );

// TODO: SPEC
export const computeLongestWeaponLength = (items: Item[]) => {
  return items.reduce((result, item) => {
    if (
      [ItemType.OneHandedWeapon, ItemType.TwoHandedWeapon, ItemType.Polearm].includes(item.type) &&
      item.weapons.length !== 0
    ) {
      return Math.max(result, item.weapons[0].length);
    }

    return result;
  }, 0 as number);
};

// TODO: handle upgrade items.
// TODO: SPEC
export const computeOverallAverageRepairCostByHour = (items: Item[]) =>
  Math.floor(items.reduce((total, item) => total + computeAverageRepairCostPerHour(item.price), 0));

export const getHeirloomPointByLevel = (level: number) =>
  level < minimumRetirementLevel ? 0 : Math.pow(2, level - minimumRetirementLevel);

export type HeirloomPointByLevelAggregation = { level: number[]; points: number };

export const getHeirloomPointByLevelAggregation = () =>
  range(minimumRetirementLevel, maximumLevel).reduce((out, level) => {
    const points = getHeirloomPointByLevel(level);
    const idx = out.findIndex(item => item.points === points);

    if (idx === -1) {
      out.push({ points, level: [level] });
    } else {
      out[idx].level.push(level);
    }

    return out;
  }, [] as HeirloomPointByLevelAggregation[]);

export const getExperienceMultiplierBonus = (multiplier: number) => {
  if (multiplier < maxExperienceMultiplierForGeneration) {
    return experienceMultiplierByGeneration;
  }

  return 0;
};

export interface RespecCapability {
  enabled: boolean;
  price: number;
  nextFreeAt: number;
  freeRespecWindowRemain: number;
}

export const getRespecCapability = (
  character: Character,
  limitations: CharacterLimitations,
  userGold: number,
  isRecentUser: boolean
): RespecCapability => {
  if (isRecentUser || character.forTournament) {
    return {
      enabled: true,
      price: 0,
      freeRespecWindowRemain: 0,
      nextFreeAt: 0,
    };
  }

  const freeRespecWindow = new Date(limitations.lastRespecializeAt);
  freeRespecWindow.setUTCHours(freeRespecWindow.getUTCHours() + freeRespecializePostWindowHours);

  if (freeRespecWindow > new Date()) {
    return {
      enabled: true,
      price: 0,
      freeRespecWindowRemain: computeLeftMs(freeRespecWindow, 0),
      nextFreeAt: 0,
    };
  }

  const lastRespecDate = new Date(limitations.lastRespecializeAt);
  const nextFreeAt = new Date(limitations.lastRespecializeAt);
  nextFreeAt.setUTCDate(nextFreeAt.getUTCDate() + freeRespecializeIntervalDays);
  nextFreeAt.setUTCMinutes(nextFreeAt.getUTCMinutes() + 5); // 5 minute margin just in case

  if (nextFreeAt < new Date()) {
    return { enabled: true, price: 0, freeRespecWindowRemain: 0, nextFreeAt: 0 };
  }

  const decayDivider =
    (new Date().getTime() - lastRespecDate.getTime()) / (respecializePriceHalfLife * 1000 * 3600);

  const price = Math.floor(
    Math.floor((character.experience / getExperienceForLevel(30)) * respecializePriceForLevel30) /
      Math.pow(2, decayDivider)
  );

  return {
    enabled: price <= userGold,
    price,
    nextFreeAt: computeLeftMs(nextFreeAt, 0),
    freeRespecWindowRemain: 0,
  };
};

export const getCharacterSLotsSchema = (): {
  key: ItemSlot;
  placeholderIcon: string;
}[][] => [
  // left col
  [
    {
      key: ItemSlot.Head,
      placeholderIcon: 'item-type-head-armor',
    },
    {
      key: ItemSlot.Shoulder,
      placeholderIcon: 'item-type-shoulder-armor',
    },
    {
      key: ItemSlot.Body,
      placeholderIcon: 'item-type-body-armor',
    },
    {
      key: ItemSlot.Hand,
      placeholderIcon: 'item-type-hand-armor',
    },
    {
      key: ItemSlot.Leg,
      placeholderIcon: 'item-type-leg-armor',
    },
  ],
  // center col
  [
    {
      key: ItemSlot.MountHarness,
      placeholderIcon: 'item-type-mount-harness',
    },
    {
      key: ItemSlot.Mount,
      placeholderIcon: 'item-type-mount',
    },
  ],
  // right col
  [
    {
      key: ItemSlot.Weapon0,
      placeholderIcon: 'weapons',
    },
    {
      key: ItemSlot.Weapon1,
      placeholderIcon: 'weapons',
    },
    {
      key: ItemSlot.Weapon2,
      placeholderIcon: 'weapons',
    },
    {
      key: ItemSlot.Weapon3,
      placeholderIcon: 'weapons',
    },
    {
      key: ItemSlot.WeaponExtra,
      placeholderIcon: 'item-flag-drop-on-change',
    },
  ],
];

export const getCharacterKDARatio = (characterStatistics: CharacterStatistics) => {
  let _deaths = characterStatistics.deaths;
  if (characterStatistics.deaths === 0) {
    _deaths = 1;
  }

  return (
    Math.round((100 * (characterStatistics.kills + characterStatistics.assists)) / _deaths) / 100
  );
};

export const characterClassToIcon: Record<CharacterClass, string> = {
  [CharacterClass.Peasant]: 'char-class-peasant',
  [CharacterClass.Infantry]: 'weapon-class-one-handed-polearm',
  [CharacterClass.ShockInfantry]: 'weapon-class-two-handed-axe',
  [CharacterClass.Skirmisher]: 'weapon-class-throwing-spear',
  [CharacterClass.Crossbowman]: 'item-type-crossbow',
  [CharacterClass.Archer]: 'item-type-bow',
  [CharacterClass.Cavalry]: 'char-class-cav',
  [CharacterClass.MountedArcher]: 'char-class-ha',
};

// TODO: SPEC
export const getOverallArmorValueBySlot = (
  slot: ItemSlot,
  itemsStats: CharacterOverallItemsStats
) => {
  const itemSlotToArmorValue: Partial<Record<ItemSlot, CharacterArmorOverall>> = {
    [ItemSlot.Shoulder]: {
      key: CharacterArmorOverallKey.HeadArmor,
      value: itemsStats.headArmor,
    },
    [ItemSlot.Body]: {
      key: CharacterArmorOverallKey.BodyArmor,
      value: itemsStats.bodyArmor,
    },
    [ItemSlot.Hand]: {
      key: CharacterArmorOverallKey.ArmArmor,
      value: itemsStats.armArmor,
    },
    [ItemSlot.Leg]: {
      key: CharacterArmorOverallKey.LegArmor,
      value: itemsStats.legArmor,
    },
    [ItemSlot.Mount]: {
      key: CharacterArmorOverallKey.MountArmor,
      value: itemsStats.mountArmor,
    },
  };

  return slot in itemSlotToArmorValue ? itemSlotToArmorValue[slot] : undefined;
};

// TODO: SPEC, more complicated logic?
export const checkUpkeepIsHigh = (userGold: number, upkeepPerHour: number) => {
  return userGold < upkeepPerHour * 2.5;
};

export const validateItemNotMeetRequirement = (
  item: Item,
  characterCharacteristics: CharacterCharacteristics
) => {
  return item.requirement > characterCharacteristics.attributes.strength;
};
