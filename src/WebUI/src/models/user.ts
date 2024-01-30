import { Platform } from './platform';
import Role from './role';
import { Region } from './region';
import { ItemSlot, ItemType, type Item } from './item';
import { type Clan } from './clan';

export interface User {
  id: number;
  platform: Platform;
  platformUserId: string;
  name: string;
  gold: number;
  heirloomPoints: number;
  role: Role;
  avatar: string;
  activeCharacterId: number | null;
  region: Region;
  experienceMultiplier: number;
  isDonor: boolean;
}

export interface UserPublic
  extends Pick<User, 'id' | 'platform' | 'platformUserId' | 'name' | 'region'> {
  avatar: string;
  clan: Clan | null;
}

export interface UserPrivate extends UserPublic {
  createdAt: Date;
  updatedAt: Date;
  gold: number;
  note: string;
  activeCharacterId: number | null;
}

// TODO: to /models/item.ts
export interface UserItem {
  id: number;
  userId: number;
  createdAt: Date;
  item: Item;
  isBroken: boolean;
  isArmoryItem: boolean;
}

export interface UserItemsByType {
  type: ItemType;
  items: UserItem[];
}

export type UserItemsBySlot = Record<ItemSlot, UserItem>;
