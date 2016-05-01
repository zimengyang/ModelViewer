using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.Windows.Forms;
using Common.Libs.MiscFunctions;

namespace MeshFlowViewer
{
    [Serializable]
    public class PropertyBag : INotifyPropertyChanged
    {
        public List<NotifyProperty> Properties = new List<NotifyProperty>();
        private List<String> ChangedPropertyNames = new List<String>();

        public PropertyBag(params NotifyProperty[] props) : base()
        {
            foreach (NotifyProperty prop in props) AddProperty(prop);
        }

        protected int iDeferPropertyChanged = 0;
        protected bool bPropertyChangedFired = false;
        public bool DeferPropertyChanged
        {
            get { return iDeferPropertyChanged > 0; }
            set
            {
                if (value) { iDeferPropertyChanged++; return; }
                if (iDeferPropertyChanged > 0) iDeferPropertyChanged--;
                if (iDeferPropertyChanged == 0 && bPropertyChangedFired) SendPropertyChanged();
            }
        }

        public NotifyProperty AddProperty(NotifyProperty prop)
        {
            Properties.Add(prop);
            prop.PropertyChanged += delegate (object sender, PropertyChangedEventArgs e) {
                SendPropertyChanged(e.PropertyName);
            };
            return prop;
        }
        public void AddProperties(params NotifyProperty[] props)
        {
            foreach (NotifyProperty prop in props) AddProperty(prop);
        }
        /*public void AddProperties( params NotifyProperty[] props )
		{
			foreach( NotifyProperty prop in props ) AddProperty( prop );
		}*/

        public NotifyProperty GetProperty(String name)
        {
            foreach (NotifyProperty prop in Properties)
                if (prop.Name == name) return prop;
            return null;
        }

        public void RemoveProperty(NotifyProperty prop) { Properties.Remove(prop); }

        public void RemoveProperty(String name) { RemoveProperty(GetProperty(name)); }

        public event PropertyChangedEventHandler PropertyChanged;
        public void SendPropertyChanged(String name) { ChangedPropertyNames.Add(name); SendPropertyChanged(); }
        public void SendPropertyChanged()
        {
            if (DeferPropertyChanged) { bPropertyChangedFired = true; return; }
            bPropertyChangedFired = false;

            if (PropertyChanged != null)
            {
                //foreach( String name in ChangedPropertyNames )
                //	PropertyChanged( this, new PropertyChangedEventArgs( name ) );
                PropertyChanged(this, new PropertyChangedEventArgs(String.Join(",", ChangedPropertyNames.ToArray())));
            }

            ChangedPropertyNames.Clear();
        }
    }

    [Serializable]
    public abstract class NotifyProperty : INotifyPropertyChanged, IXmlSerializable
    {
        protected String name;
        public String Name { get { return name; } }

        protected bool bIgnorePropertyChanged = false;
        protected bool bDeferPropertyChanged = false;
        protected bool bPropertyChangedFired = false;
        public bool DeferPropertyChanged
        {
            get { return bDeferPropertyChanged; }
            set
            {
                bDeferPropertyChanged = value;
                if (!bDeferPropertyChanged && bPropertyChangedFired) SendPropertyChanged();
            }
        }
        public bool IgnorePropertyChanges
        {
            get { return bIgnorePropertyChanged; }
            set { bIgnorePropertyChanged = value; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void SendPropertyChanged()
        {
            if (bIgnorePropertyChanged) return;
            if (bDeferPropertyChanged) { bPropertyChangedFired = true; return; }
            bPropertyChangedFired = false;
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        public NotifyProperty(String name) { this.name = name; }

        public XmlSchema GetSchema() { return null; }
        public abstract void ReadXml(XmlReader reader);
        public abstract void WriteXml(XmlWriter writer);
    }

    [Serializable]
    public class Property<T> : NotifyProperty
    {
        protected T val;
        protected T oldval;

        public Property(String name, T initval) : base(name) { this.val = initval; }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteString(val.ToString());
        }
        public override void ReadXml(XmlReader reader)
        {
            string text = reader.ReadString();
            val = text.To<T>();
        }

        public virtual T Val { get { return val; } set { oldval = val; val = value; SendPropertyChanged(); } }
        public virtual T Get() { return val; }
        public virtual void Set(T newval) { oldval = val; val = newval; SendPropertyChanged(); }
        public virtual void SetWithoutEvent(T newval) { oldval = val; val = newval; }
        public T GetOldVal() { return oldval; }

        public Type GetPropertyType() { return typeof(T); } // val.GetType(); }

        public static implicit operator T(Property<T> prop) { return prop.val; }
    }

    [Serializable]
    public class PropertyBool : Property<bool>
    {
        public PropertyBool(String name, bool initval) : base(name, initval) { }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteString(val.ToString());
        }
        public override void ReadXml(XmlReader reader)
        {
            string text = reader.ReadString();
            val = text.To<bool>();
        }
        public void Toggle() { Set(!val); }

    }

    [Serializable]
    public class PropertyArray<T> : NotifyProperty
    {
        protected T[] vals;
        public List<int> changed = new List<int>();
        protected int size;

        public PropertyArray(String name, int size) : base(name)
        {
            this.size = size;
            vals = new T[size];
        }

        public override void WriteXml(XmlWriter writer)
        {
            XmlSerializer x = new XmlSerializer(typeof(T[]));
            x.Serialize(writer, vals);
        }

        public override void ReadXml(XmlReader reader)
        {
            XmlSerializer x = new XmlSerializer(typeof(T[]));
            vals = (T[])x.Deserialize(reader);
        }

        public T this[int index]
        {
            get { return vals[index]; }
            set
            {
                vals[index] = value;
                if (!base.bDeferPropertyChanged) changed.Clear();
                changed.Add(index);
                SendPropertyChanged();

            }
        }
        public T Get(int index) { return vals[index]; }
        public void Set(int index, T newval) { vals[index] = newval; SendPropertyChanged(); }

        public void SetArray(T[] newvals)
        {
            size = newvals.Length;
            vals = newvals;
            SendPropertyChanged();
        }

        public int Size
        {
            get { return size; }
            set
            {
                size = value;
                vals = new T[value];
                changed.Clear();
                SendPropertyChanged();
            }
        }

        public T[] GetArray() { return vals; }

        public static implicit operator T[] (PropertyArray<T> proparray)
        {
            return proparray.GetArray();
        }
        //public static explicit operator T[] (PropertyArray<T> proparray) { return proparray.vals; }
    }

    [Serializable]
    public class PropertyList<T> : NotifyProperty
    {
        protected List<T> vals = new List<T>();

        public PropertyList(String name) : base(name)
        {
        }

        public override void WriteXml(XmlWriter writer)
        {
            XmlSerializer x = new XmlSerializer(typeof(List<T>));
            x.Serialize(writer, vals);
        }

        public override void ReadXml(XmlReader reader)
        {
            XmlSerializer x = new XmlSerializer(typeof(List<T>));
            vals = (List<T>)x.Deserialize(reader);
        }

        public T this[int index]
        {
            get { return vals[index]; }
        }

        public T Get(int index)
        {
            return vals[index];
        }

        public void Add(T nval)
        {
            vals.Add(nval);
            SendPropertyChanged();
        }

        public void RemoveAt(int index)
        {
            vals.RemoveAt(index);
            SendPropertyChanged();
        }

        public static implicit operator List<T>(PropertyList<T> proplist) { return proplist.vals; }
    }

    public delegate T ValidationFunction<T>(T newval, T oldval);

    [Serializable]
    public class PropertyValidated<T> : Property<T>
    {
        protected ValidationFunction<T> fnValidation;

        public PropertyValidated(String name, T initval, ValidationFunction<T> fnValidation)
            : base(name, initval)
        {
            this.fnValidation = fnValidation;
        }

        public override T Val
        {
            get { return val; }
            set { oldval = val; val = fnValidation(value, oldval); SendPropertyChanged(); }
        }
        public override T Get() { return val; }
        public override void Set(T newval)
        {
            oldval = val;
            val = fnValidation(newval, oldval);
            SendPropertyChanged();
        }
        public override void SetWithoutEvent(T newval) { oldval = val; val = fnValidation(newval, oldval); }
    }

    [Serializable]
    public abstract class PropertyConstrained<T> : Property<T>
    {
        protected Property<T> min;
        protected Property<T> max;
        protected PropertyBag pbag = new PropertyBag();

        public T Minimum
        {
            get { return min; }
            set { min.Set(value); }
        }
        public T Maximum
        {
            get { return max; }
            set { max.Set(value); }
        }

        public NotifyProperty MinimumProperty { get { return min; } }
        public NotifyProperty MaximumProperty { get { return max; } }

        protected abstract void HandleConstraint();

        public PropertyConstrained(String name, T initval, T minval, T maxval) : base(name, initval)
        {
            min = new Property<T>("Minimum", minval);
            max = new Property<T>("Maximum", maxval);
            pbag.AddProperty(min);
            pbag.AddProperty(max);
            pbag.AddProperty(this);
            pbag.PropertyChanged += delegate { HandleConstraint(); };
        }

        public override void WriteXml(XmlWriter writer)
        {
            XmlSerializer x = new XmlSerializer(typeof(PropertyBag));
            x.Serialize(writer, pbag);
        }

        public override void ReadXml(XmlReader reader)
        {
            XmlSerializer x = new XmlSerializer(typeof(PropertyBag));
            pbag = (PropertyBag)x.Deserialize(reader);
        }

    }

    [Serializable]
    public class PropertyConstrainedDouble : PropertyConstrained<double>
    {
        protected override void HandleConstraint()
        {
            if (Val < min) Val = min;
            if (Val > max) Val = max;
        }

        public PropertyConstrainedDouble(String name, double initval, double minval, double maxval) : base(name, initval, minval, maxval) { }
    }

    [Serializable]
    public class PropertyConstrainedFloat : PropertyConstrained<float>
    {
        protected override void HandleConstraint()
        {
            if (Val < min) Val = min;
            if (Val > max) Val = max;
        }

        public PropertyConstrainedFloat(String name, float initval, float minval, float maxval) : base(name, initval, minval, maxval) { }
    }

    [Serializable]
    public class PropertyConstrainedInt : PropertyConstrained<int>
    {
        protected override void HandleConstraint()
        {
            if (Val < min) Val = min;
            if (Val > max) Val = max;
        }

        public PropertyConstrainedInt(String name, int initval, int minval, int maxval) : base(name, initval, minval, maxval) { }
    }
}

