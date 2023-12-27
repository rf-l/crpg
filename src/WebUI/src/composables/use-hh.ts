import { computedWithControl, useLocalStorage } from '@vueuse/core';
import { Region } from '@/models/region';
import { getHHEventByRegion, getHHEventRemaining } from '@/services/hh-service';
import { useUserStore } from '@/stores/user';
import { notify } from '@/services/notification-service';
import { t } from '@/services/translate-service';

export const useHappyHours = () => {
  const HHPollId = Symbol('hh');

  const userStore = useUserStore();

  const source = ref();
  const HHEvent = computedWithControl(
    () => source.value,
    () => getHHEventByRegion(userStore.user!.region || Region.Eu)
  );

  const HHEventRemaining = computed(() => getHHEventRemaining(HHEvent.value));

  const isHHCountdownEnded = ref<boolean>(false);
  const alreadyShownHHStartedNotification = useLocalStorage('hh-start-notification-shown', false);

  const onStartHHCountdown = () => {
    if (!alreadyShownHHStartedNotification.value) {
      notify(t('hh.notify.started'));
      alreadyShownHHStartedNotification.value = true;
    }
  };

  const onEndHHCountdown = () => {
    isHHCountdownEnded.value = true;
    alreadyShownHHStartedNotification.value = false;
    notify(t('hh.notify.ended'));
  };

  // https://fengyuanchen.github.io/vue-countdown/
  const transformSlotProps = (props: Record<string, number>) =>
    Object.entries(props).reduce(
      (out, [key, value]) => {
        out[key] = value < 10 ? `0${value}` : String(value);
        return out;
      },
      {} as Record<string, string>
    );

  return {
    HHPollId,
    HHEvent,
    HHEventRemaining,
    isHHCountdownEnded,
    onStartHHCountdown,
    onEndHHCountdown,
    transformSlotProps,
  };
};
