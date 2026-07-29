import type { FC } from "react";
import {
  JamlGameCard as JamlGameCardImpl,
  type JamlGameCardProps as ImplJamlGameCardProps,
} from "../../components/GameCard.js";

/**
 * Game Card wrapper — bridges json-render to jaml-ui's sprite system.
 */

export interface JamlGameCardProps {
  type: "joker" | "consumable" | "playing";
  card: {
    name: string;
    edition?: string;
    seal?: string;
    isEternal?: boolean;
    isPerishable?: boolean;
    isRental?: boolean;
    scale?: number;
  };
  className?: string;
}

export const JamlGameCard: FC<JamlGameCardProps> = ({
  type,
  card,
  className = "",
}) => {
  return (
    <JamlGameCardImpl
      type={type}
      card={card as ImplJamlGameCardProps["card"]}
      className={className}
    />
  );
};
