﻿using NUnit.Framework;
using SS3D.Systems.Inventory.Containers;
using SS3D.Systems.Tile;
using System.Collections;
using System.Collections.Generic;
using System.Electricity;
using System.Reflection.Emit;
using UnityEngine;

namespace EditorTests
{
    public class CircuitTests
    {
        private const float Tolerance = 0.0000001f;
        
        /// <summary>
        /// Check if batteries charge at equal rate, given they're not full. If they're close to be full, 
        /// check that they're provided with the amount needed to fill them fully, instead of the equal rate.
        /// </summary>
        [Test]
        public void BatteriesChargeAtEqualRatesOrGetFull()
        {
            BasicBattery batteryOne = CreateBasicBattery(5f, 50f, 0f);
            BasicBattery batteryTwo = CreateBasicBattery(5f, 50f, 45f);
            BasicBattery batteryThree = CreateBasicBattery(5f, 50f, 0f);
            BasicPowerGenerator generator = CreateBasicGenerator(9f);

            List<IElectricDevice> electricDevices = new List<IElectricDevice>() { batteryOne, batteryTwo, batteryThree, generator };
            Circuit circuit = CreateCircuit(electricDevices);
            circuit.UpdateCircuitPower();

            Assert.That(batteryOne.StoredPower, Is.EqualTo(batteryThree.StoredPower).Within(Tolerance));
            Assert.That(batteryOne.StoredPower, Is.EqualTo(3f).Within(Tolerance));

            circuit.UpdateCircuitPower();

            Assert.That(batteryOne.StoredPower, Is.EqualTo(batteryThree.StoredPower).Within(Tolerance));
            Assert.That(batteryOne.StoredPower, Is.EqualTo(6.5f).Within(Tolerance));
        }

        /// <summary>
        /// Check that batteries that are off don't send power to consumers.
        /// </summary>
        [Test]
        public void BatteriesOffDontProvidePower()
        {
            BasicBattery batteryOne = CreateBasicBattery(5f, 50f, 50f);
            batteryOne.IsOn = false;
            BasicPowerConsumer consumerOne = CreateBasicConsumer(1f);

            List<IElectricDevice> electricDevices = new List<IElectricDevice>() { batteryOne, consumerOne };
            Circuit circuit = CreateCircuit(electricDevices);
            circuit.UpdateCircuitPower();

            Assert.IsTrue(batteryOne.StoredPower == 50f);
        }

        /// <summary>
        /// Check that a battery with a smaller max power rate than a consumer need cannot make the consumer powered.
        /// </summary>
        [Test]
        public void BatteryCannotSendPowerAboveItsMaxRate()
        {
            BasicBattery batteryOne = CreateBasicBattery(5f, 50f, 50f);
            batteryOne.IsOn = true;
            BasicPowerConsumer consumerOne = CreateBasicConsumer(15f);

            List<IElectricDevice> electricDevices = new List<IElectricDevice>() { batteryOne, consumerOne };
            Circuit circuit = CreateCircuit(electricDevices);
            circuit.UpdateCircuitPower();

            Assert.IsTrue(consumerOne.PowerStatus == PowerStatus.Inactive);
        }

        /// <summary>
        /// Check that power generated by a bunch of generators, goes first to consumers, and only what's left goes to batteries.
        /// Test if the right amount 
        /// </summary>
        [Test]
        public void PowerGeneratedGoesFirstToConsumers()
        {
            BasicPowerGenerator generator = CreateBasicGenerator(5f);
            BasicPowerConsumer consumerOne = CreateBasicConsumer(2f);
            BasicPowerConsumer consumerTwo = CreateBasicConsumer(2f);
            BasicBattery batteryOne = CreateBasicBattery(5f, 50f, 0f);

            List<IElectricDevice> electricDevices = new List<IElectricDevice>() { generator, consumerOne, consumerTwo, batteryOne };
            Circuit circuit = CreateCircuit(electricDevices);
            circuit.UpdateCircuitPower();

            Assert.That(batteryOne.StoredPower, Is.EqualTo(1f).Within(Tolerance));
        }

        /// <summary>
        /// Check that consumers are inactive when there's not enough power generated.
        /// </summary>
        [Test]
        public void TurnOffConsumersWhenNotEnoughPowerIsGenerated()
        {
            BasicPowerGenerator generator = CreateBasicGenerator(5f);
            BasicPowerConsumer consumerOne = CreateBasicConsumer(7f);
            BasicPowerConsumer consumerTwo = CreateBasicConsumer(2f);

            List<IElectricDevice> electricDevices = new List<IElectricDevice>() { generator, consumerOne, consumerTwo};
            Circuit circuit = CreateCircuit(electricDevices);
            circuit.UpdateCircuitPower();

            Assert.IsTrue(consumerOne.PowerStatus == PowerStatus.Inactive && consumerTwo.PowerStatus == PowerStatus.Powered);
        }

        /// <summary>
        /// Check that batteries amount of watt stays between the max allowed amount and level zero.
        /// </summary>
        [Test]
        public void BatteryLevelStayBetweenMaxAmountAndZero()
        {
            BasicBattery batteryOne = CreateBasicBattery(5f, 50f, 0f);
            batteryOne.AddPower(500f);
            Assert.IsTrue(batteryOne.StoredPower == 50f);
            batteryOne.RemovePower(500f);
            Assert.IsTrue(batteryOne.StoredPower == 0f);
        }

        /// <summary>
        /// Check that adding more power than possible to a battery returns the correct amount of power added.
        /// Also check that it adds everything when possible.
        /// </summary>
        [Test]
        public void AddingPowerToBatteryReturnsTheCorrectAmountOfPowerAdded()
        {
            BasicBattery batteryOne = CreateBasicBattery(5f, 50f, 0f);
            float added = batteryOne.AddPower(47f);
            Assert.IsTrue(added == 47f);
            added = batteryOne.AddPower(50f);
            Assert.IsTrue(added == 3f);
            added = batteryOne.AddPower(50f);
            Assert.IsTrue(added == 0f);
        }

        /// <summary>
        /// Check that removing more power than possible to a battery returns the correct amount of power removed.
        /// Also check that it removes everything when possible.
        /// </summary>
        [Test]
        public void RemovingPowerFromBatteryReturnsTheCorrectAmountOfPowerRemoved()
        {
            BasicBattery batteryOne = CreateBasicBattery(5f, 50f, 50f);
            float removed = batteryOne.RemovePower(47f);
            Assert.IsTrue(removed == 47f);
            removed = batteryOne.RemovePower(50f);
            Assert.IsTrue(removed == 3f);
            removed = batteryOne.RemovePower(50f);
            Assert.IsTrue(removed == 0f);
        }
        
        /// <summary>
        /// Check if two consumers have lower needs than battery max power rate, but their total needs are above it, only one consumer is powered.
        /// </summary>
        [Test]
        public void BatteryMaxPowerRateIsUsedProperly()
        {
            BasicBattery batteryOne = CreateBasicBattery(3f, 50f, 50f);
            batteryOne.IsOn = true;
            BasicPowerConsumer consumerOne = CreateBasicConsumer(2f);
            BasicPowerConsumer consumerTwo = CreateBasicConsumer(2f);

            List<IElectricDevice> electricDevices = new List<IElectricDevice>() { batteryOne, consumerOne, consumerTwo };
            Circuit circuit = CreateCircuit(electricDevices);

            // ACT
            circuit.UpdateCircuitPower();

            // ASSERT
            Assert.That(batteryOne.StoredPower, Is.EqualTo(48f).Within(Tolerance));
            Assert.IsTrue(consumerOne.PowerStatus == PowerStatus.Powered ^ consumerTwo.PowerStatus == PowerStatus.Powered);
        }

        /// <summary>
        /// Check if batteries can send power to consumers even though consumers need more power than batteries can provide.
        /// </summary>
        [Test]
        public void BatteriesSendPowerDespiteNotCoveringAllConsumersNeed()
        {
            // ARRANGE
            BasicBattery batteryOne = CreateBasicBattery(5f, 50f, 50f);
            BasicBattery batteryTwo = CreateBasicBattery(5f, 50f, 50f);
            batteryOne.IsOn = true;
            batteryTwo.IsOn = true;
            BasicPowerConsumer consumerOne = CreateBasicConsumer(2f);
            BasicPowerConsumer consumerTwo = CreateBasicConsumer(7f);
            
            List<IElectricDevice> electricDevices = new List<IElectricDevice>() { batteryOne, batteryTwo, consumerOne, consumerTwo};
            Circuit circuit = CreateCircuit(electricDevices);

            // ACT
            circuit.UpdateCircuitPower();

            // ASSERT
            Assert.IsTrue(batteryOne.StoredPower < 50f);
            Assert.IsTrue(batteryTwo.StoredPower < 50f);
        }

        [Test]
        public void BatteryCanPowerMultipleConsumersPerUpdate()
        {
            BasicBattery batteryOne = CreateBasicBattery(5f, 50f, 50f);
            batteryOne.IsOn = true;
            BasicPowerConsumer consumerOne = CreateBasicConsumer(2f);
            BasicPowerConsumer consumerTwo = CreateBasicConsumer(2f);

            List<IElectricDevice> electricDevices = new List<IElectricDevice>() { batteryOne, consumerOne, consumerTwo };
            Circuit circuit = CreateCircuit(electricDevices);

            // ACT
            circuit.UpdateCircuitPower();

            // ASSERT
            Assert.That(batteryOne.StoredPower, Is.EqualTo(46f).Within(Tolerance));
            Assert.IsTrue(consumerOne.PowerStatus == PowerStatus.Powered && consumerTwo.PowerStatus == PowerStatus.Powered);
        }

        private static Circuit CreateCircuit(List<IElectricDevice> electricDevices)
        {
            Circuit circuit = new Circuit();
            electricDevices.ForEach(x => circuit.AddElectricDevice(x));
            return circuit;
        }

        private static BasicBattery CreateBasicBattery(float maxPowerRate, float maxCapacity, float storedPower)
        {
            GameObject batteryGo = new GameObject();
            batteryGo.AddComponent<BasicBattery>();
            batteryGo.AddComponent<PlacedTileObject>();
            BasicBattery battery = batteryGo.GetComponent<BasicBattery>();
            battery.Init(maxPowerRate, maxCapacity, storedPower);
            return batteryGo.GetComponent<BasicBattery>();
        }

        private static BasicPowerConsumer CreateBasicConsumer(float powerConsumption)
        {
            GameObject batteryGo = new GameObject();
            batteryGo.AddComponent<BasicPowerConsumer>();
            batteryGo.AddComponent<PlacedTileObject>();
            BasicPowerConsumer consumer = batteryGo.GetComponent<BasicPowerConsumer>();
            consumer.Init(powerConsumption);
            return consumer;
        }

        private static BasicPowerGenerator CreateBasicGenerator(float generatedPower)
        {
            GameObject generatorGo = new GameObject();
            generatorGo.AddComponent<BasicPowerGenerator>();
            generatorGo.AddComponent<PlacedTileObject>();
            BasicPowerGenerator generator = generatorGo.GetComponent<BasicPowerGenerator>();

            generator.PowerProduction = generatedPower;
            return generatorGo.GetComponent<BasicPowerGenerator>();
        }
    }
}