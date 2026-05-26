export const ritualConfig = {
    title: "The Daily Wee",
    tagline: "A curated daily Balatro ritual",
    id: "TheDailyWee",
    defaultObjective: "Wee Joker",
    /**
     * The Weepoch (Jan 30, 2026)
     * This is the canonical start date for Day 1.
     */
    epochDate: "2026-02-02T00:00:00Z",
    get epoch() {
        return new Date(this.epochDate).getTime();
    }
};

export const EPOCH = ritualConfig.epoch;
