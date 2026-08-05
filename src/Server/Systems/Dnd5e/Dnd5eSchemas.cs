namespace Vtt.Server.Systems.Dnd5e;

/// <summary>
/// The document shapes this version of the module promises.
/// </summary>
/// <remarks>
/// Every field here is a promise that a later version has to migrate. Deliberately narrow: a small
/// schema that is right beats a large one that needs a breaking change in a fortnight.
/// <para>
/// Raw and derived are separate objects on purpose. Ability scores are what a player chooses;
/// modifiers, saves, skill totals and passive perception are computed from them by
/// <c>RecomputeDerived</c>. Keeping them apart makes it obvious which is which, and makes it
/// visible when a derived value has been written by hand.
/// </para>
/// </remarks>
internal static class Dnd5eSchemas
{
    public const string CharacterSheet =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "required": ["identity", "abilities", "proficiencyBonus", "hitPoints", "armourClass"],
          "additionalProperties": false,
          "properties": {
            "identity": {
              "type": "object",
              "required": ["name"],
              "additionalProperties": false,
              "properties": {
                "name": { "type": "string", "minLength": 1, "maxLength": 80 },
                "class": { "type": "string", "maxLength": 40 },
                "level": { "type": "integer", "minimum": 1, "maximum": 20 }
              }
            },
            "abilities": {
              "type": "object",
              "required": ["strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma"],
              "additionalProperties": false,
              "properties": {
                "strength": { "$ref": "#/$defs/abilityScore" },
                "dexterity": { "$ref": "#/$defs/abilityScore" },
                "constitution": { "$ref": "#/$defs/abilityScore" },
                "intelligence": { "$ref": "#/$defs/abilityScore" },
                "wisdom": { "$ref": "#/$defs/abilityScore" },
                "charisma": { "$ref": "#/$defs/abilityScore" }
              }
            },
            "proficiencyBonus": { "type": "integer", "minimum": 2, "maximum": 6 },
            "hitPoints": {
              "type": "object",
              "required": ["current", "maximum"],
              "additionalProperties": false,
              "properties": {
                "current": { "type": "integer" },
                "maximum": { "type": "integer", "minimum": 1 },
                "temporary": { "type": "integer", "minimum": 0 }
              }
            },
            "armourClass": { "type": "integer", "minimum": 1, "maximum": 40 },
            "savingThrowProficiencies": { "$ref": "#/$defs/abilityList" },
            "skillProficiencies": {
              "type": "array",
              "uniqueItems": true,
              "items": { "$ref": "#/$defs/skill" }
            },
            "derived": {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "abilityModifiers": { "type": "object", "additionalProperties": { "type": "integer" } },
                "savingThrows": { "type": "object", "additionalProperties": { "type": "integer" } },
                "skills": { "type": "object", "additionalProperties": { "type": "integer" } },
                "passivePerception": { "type": "integer" }
              }
            }
          },
          "$defs": {
            "abilityScore": { "type": "integer", "minimum": 1, "maximum": 30 },
            "ability": {
              "type": "string",
              "enum": ["strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma"]
            },
            "abilityList": { "type": "array", "uniqueItems": true, "items": { "$ref": "#/$defs/ability" } },
            "skill": {
              "type": "string",
              "enum": [
                "acrobatics", "animalHandling", "arcana", "athletics", "deception", "history",
                "insight", "intimidation", "investigation", "medicine", "nature", "perception",
                "performance", "persuasion", "religion", "sleightOfHand", "stealth", "survival"
              ]
            }
          }
        }
        """;

    /// <remarks>
    /// Minimal on purpose: nothing consumes it until task 078's ingestion pipeline, and guessing at
    /// the shape of a spell six tasks early is how a wrong schema gets locked in.
    /// </remarks>
    public const string CompendiumEntry =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "required": ["kind", "name"],
          "properties": {
            "kind": { "type": "string", "enum": ["spell", "monster", "item", "rule"] },
            "name": { "type": "string", "minLength": 1, "maxLength": 120 }
          }
        }
        """;
}
