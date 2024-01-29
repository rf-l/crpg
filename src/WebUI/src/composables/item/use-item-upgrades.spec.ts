import { createTestingPinia } from '@pinia/testing';
import { flushPromises } from '@vue/test-utils';
import { useUserStore } from '@/stores/user';
import { type ItemFlat } from '@/models/item';
import { type AggregationConfig } from '@/models/item-search';

const { mockedGetItemUpgrades, mockedGetRelativeEntries } = vi.hoisted(() => ({
  mockedGetItemUpgrades: vi.fn().mockResolvedValue([]),
  mockedGetRelativeEntries: vi.fn().mockReturnValue({}),
}));
vi.mock('@/services/item-service', () => ({
  getItemUpgrades: mockedGetItemUpgrades,
  getRelativeEntries: mockedGetRelativeEntries,
}));

const userStore = useUserStore(createTestingPinia());

import { useItemUpgrades } from './use-item-upgrades';

const mockItemUpgrades = [
  {
    id: 't0',
    rank: 0,
  },
  {
    id: 't1',
    rank: 1,
  },
  {
    id: 't2',
    rank: 2,
  },
  {
    id: 't3',
    rank: 3,
  },
];

beforeAll(() => {
  mockedGetItemUpgrades.mockResolvedValue(mockItemUpgrades);
});

beforeEach(() => {
  userStore.$reset();
  userStore.$patch({ user: { heirloomPoints: 0 }, userItems: [] });
});

const item = {
  id: 't0',
  rank: 0,
  baseId: '1',
} as ItemFlat;

const cols = {} as AggregationConfig;

describe('useItemUpgrades', () => {
  it('itemUpgrades', async () => {
    const { itemUpgrades, isLoading } = useItemUpgrades(item, cols);

    expect(isLoading.value).toEqual(true);

    await flushPromises();

    expect(itemUpgrades.value).toEqual(mockItemUpgrades);
    expect(isLoading.value).toEqual(false);
  });

  it('baseItem', async () => {
    const { baseItem } = useItemUpgrades(item, cols);

    await flushPromises();

    expect(baseItem.value).toEqual(mockItemUpgrades[0]);
  });

  it('nextItem', async () => {
    const { nextItem } = useItemUpgrades(item, cols);

    await flushPromises();

    expect(nextItem.value).toEqual(mockItemUpgrades[1]);
  });

  it('relativeEntries', async () => {
    const { relativeEntries } = useItemUpgrades(item, cols);

    await flushPromises();

    expect(relativeEntries.value).toEqual({});
  });

  describe('validation', () => {
    it('basic', async () => {
      userStore.$patch({ user: { heirloomPoints: 1 }, userItems: [] });
      const { validation, canUpgrade } = useItemUpgrades(item, cols);

      await flushPromises();

      expect(validation.value.points).toEqual(true);
      expect(validation.value.maxRank).toEqual(true);
      expect(canUpgrade.value).toEqual(true);
    });

    it('points', async () => {
      userStore.$patch({ user: { heirloomPoints: 0 }, userItems: [] });

      const { validation, canUpgrade } = useItemUpgrades(item, cols);

      await flushPromises();

      expect(validation.value.points).toEqual(false);
      expect(validation.value.maxRank).toEqual(true);
      expect(canUpgrade.value).toEqual(false);
    });

    it('maxRank', async () => {
      const item = {
        id: 't3',
        rank: 3,
        baseId: '1',
      } as ItemFlat;

      userStore.$patch({
        user: { heirloomPoints: 1 },
      });

      const { validation, canUpgrade } = useItemUpgrades(item, cols);

      await flushPromises();

      expect(validation.value.maxRank).toEqual(false);
      expect(canUpgrade.value).toEqual(false);
    });
  });
});
